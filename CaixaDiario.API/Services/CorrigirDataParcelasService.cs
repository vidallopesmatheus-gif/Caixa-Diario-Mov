using CaixaDiario.API.DTOs.Admin;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

// Money Vision: "data da parcela vs. competência" (item 2) — migração de reparo pra produção.
// ImportacaoService.ReancorarParcelasNaFatura já garante isso em toda importação nova, mas as
// parcelas importadas ANTES da reancoragem existir continuam com a Data da compra original (meses
// antes da fatura que de fato as cobrou). Move cada uma pro dia de fechamento/vencimento da sua
// Fatura real, preservando a data antiga em DataCompraOriginal. Mesmo padrão dry-run-por-padrão
// das outras migrações: sem confirmar=true, só relatório.
public class CorrigirDataParcelasService : ICorrigirDataParcelasService
{
    private readonly IContaBancariaRepository _contaRepo;
    private readonly IRegistroRepository _registroRepo;
    private readonly IFaturaRepository _faturaRepo;

    public CorrigirDataParcelasService(
        IContaBancariaRepository contaRepo, IRegistroRepository registroRepo, IFaturaRepository faturaRepo)
    {
        _contaRepo = contaRepo;
        _registroRepo = registroRepo;
        _faturaRepo = faturaRepo;
    }

    public async Task<CorrigirDataParcelasResultDto> CorrigirAsync(bool confirmar)
    {
        var resultado = new CorrigirDataParcelasResultDto { Confirmado = confirmar };
        var contasCartao = (await _contaRepo.ListarTodasAsync()).Where(c => c.Tipo == "CartaoCredito").ToList();

        foreach (var conta in contasCartao)
        {
            var registros = (await _registroRepo.ListarPorContaAsync(conta.Id)).Where(r => !r.Excluido).ToList();
            var faturasCache = new Dictionary<Guid, Fatura?>();
            var contaAfetada = false;

            foreach (var registro in registros.ToList())
            {
                foreach (var item in registro.Saidas.Where(s => s.FaturaId.HasValue).ToList())
                {
                    var parcela = ParcelaDescricaoParser.TentarExtrair(item.Descricao);
                    if (parcela == null || parcela.Numero <= 1) continue; // só parcela 2+ é cobrança da fatura

                    if (!faturasCache.TryGetValue(item.FaturaId!.Value, out var fatura))
                    {
                        fatura = await _faturaRepo.ObterPorIdAsync(item.FaturaId.Value);
                        faturasCache[item.FaturaId.Value] = fatura;
                    }
                    if (fatura == null) continue;

                    var dataEfetiva = fatura.DataFechamento ?? fatura.DataVencimento;
                    if (registro.Data == dataEfetiva) continue; // já está na data certa

                    resultado.Itens.Add(new ItemDataParcelaCorrigidoDto
                    {
                        ClienteId = conta.ClienteId, ContaBancariaId = conta.Id, ContaBancariaNome = conta.Nome,
                        Descricao = item.Descricao, Valor = item.Valor, DataAntiga = registro.Data, DataNova = dataEfetiva,
                    });

                    if (!confirmar) continue;

                    item.DataCompraOriginal ??= registro.Data;
                    registro.Saidas = registro.Saidas.Where(s => s.Id != item.Id).ToList();
                    registro.SaldoFinal += item.Valor; // devolve o valor — não é mais gasto deste dia
                    registro.SalvoEm = DateTime.UtcNow;
                    await _registroRepo.AtualizarAsync(registro);

                    var (registroNovo, novo) = await RegistroDiaHelper.ResolverOuCriarAsync(_registroRepo, conta, dataEfetiva);
                    registroNovo.Saidas = new List<ItemFinanceiroSaida>(registroNovo.Saidas) { item };
                    registroNovo.SaldoFinal -= item.Valor;
                    registroNovo.SalvoEm = DateTime.UtcNow;
                    await RegistroDiaHelper.PersistirAsync(_registroRepo, registroNovo, novo);

                    contaAfetada = true;
                }
            }

            if (confirmar && contaAfetada)
            {
                await RegistroService.RecalcularContaInteiraAsync(_registroRepo, conta.Id, conta.SaldoInicial);
                resultado.ContasRecalculadas++;
            }
        }

        resultado.ItensEncontrados = resultado.Itens.Count;
        return resultado;
    }
}
