using CaixaDiario.API.DTOs.Admin;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

// Money Vision: "fatura do C6 dividida em duas + pagamentos não vinculados" (item 3) — migração
// de reparo pra produção. Antes da reutilização de fatura por banco+competência existir
// (ImportacaoService), cada importação criava uma Fatura nova mesmo quando já havia uma pro mesmo
// banco/competência/conta — mescla essas duplicadas numa só e, depois, tenta vincular
// automaticamente um pagamento (transferência) já existente a cada fatura ainda aberta (mesma
// lógica do pós-importação, ver FaturaCartaoService.AutoVincularPagamentoPorTransferenciaAsync).
// Mesmo padrão dry-run-por-padrão das outras migrações: sem confirmar=true, só relatório (o
// auto-vínculo de pagamento só é tentado quando confirmar=true, por não ter uma versão "preview").
public class CorrigirFaturasService : ICorrigirFaturasService
{
    private readonly IContaBancariaRepository _contaRepo;
    private readonly IRegistroRepository _registroRepo;
    private readonly IFaturaRepository _faturaRepo;
    private readonly IPagamentoFaturaRepository _pagamentoRepo;
    private readonly IFaturaCartaoService _faturaCartaoService;

    public CorrigirFaturasService(
        IContaBancariaRepository contaRepo, IRegistroRepository registroRepo, IFaturaRepository faturaRepo,
        IPagamentoFaturaRepository pagamentoRepo, IFaturaCartaoService faturaCartaoService)
    {
        _contaRepo = contaRepo;
        _registroRepo = registroRepo;
        _faturaRepo = faturaRepo;
        _pagamentoRepo = pagamentoRepo;
        _faturaCartaoService = faturaCartaoService;
    }

    public async Task<CorrigirFaturasResultDto> CorrigirAsync(bool confirmar)
    {
        var resultado = new CorrigirFaturasResultDto { Confirmado = confirmar };
        var contasCartao = (await _contaRepo.ListarTodasAsync()).Where(c => c.Tipo == "CartaoCredito").ToList();

        foreach (var conta in contasCartao)
        {
            var faturas = await _faturaRepo.ListarPorContaAsync(conta.Id);
            var grupos = faturas.GroupBy(f => (Banco: f.Banco.Trim().ToUpperInvariant(), f.Competencia));

            foreach (var grupo in grupos.Where(g => g.Count() > 1))
            {
                // Mantém a mais antiga (a que o usuário via primeiro) — as outras são as
                // "sobressalentes" criadas por reimportações antes da reutilização existir.
                var lista = grupo.OrderBy(f => f.CriadoEm).ToList();
                var manter = lista[0];

                foreach (var duplicada in lista.Skip(1))
                {
                    resultado.Mesclagens.Add(new FaturaMescladaDto
                    {
                        ClienteId = conta.ClienteId, ContaBancariaId = conta.Id, Banco = manter.Banco,
                        Competencia = manter.Competencia, FaturaMantidaId = manter.Id, FaturaRemovidaId = duplicada.Id,
                    });

                    if (!confirmar) continue;

                    var registros = await _registroRepo.ListarPorContaAsync(conta.Id);
                    foreach (var registro in registros.Where(r => r.Saidas.Any(s => s.FaturaId == duplicada.Id)))
                    {
                        foreach (var item in registro.Saidas.Where(s => s.FaturaId == duplicada.Id))
                            item.FaturaId = manter.Id;
                        registro.Saidas = new List<ItemFinanceiroSaida>(registro.Saidas);
                        registro.SalvoEm = DateTime.UtcNow;
                        await _registroRepo.AtualizarAsync(registro);
                    }

                    var pagamentos = await _pagamentoRepo.ListarPorContaCartaoAsync(conta.Id);
                    foreach (var pagamento in pagamentos.Where(p => p.FaturaId == duplicada.Id))
                    {
                        pagamento.FaturaId = manter.Id;
                        await _pagamentoRepo.AtualizarAsync(pagamento);
                    }

                    await _faturaRepo.RemoverAsync(duplicada);
                }
            }
        }
        resultado.FaturasMescladas = resultado.Mesclagens.Count;

        if (!confirmar) return resultado;

        // Só depois de mesclar: tenta vincular um pagamento já existente a cada fatura que ainda
        // estiver com saldo devedor (nunca cria lançamento novo, nunca decide se houver ambiguidade
        // — ver AutoVincularPagamentoPorTransferenciaAsync).
        foreach (var conta in contasCartao)
        {
            foreach (var fatura in await _faturaRepo.ListarPorContaAsync(conta.Id))
            {
                if (await _faturaCartaoService.AutoVincularPagamentoPorTransferenciaAsync(conta.Id, fatura.Id))
                    resultado.PagamentosVinculados++;
            }
        }

        return resultado;
    }
}
