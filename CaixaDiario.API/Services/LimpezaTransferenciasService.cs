using CaixaDiario.API.DTOs.Admin;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

// Correção do bug "desfazer transferência não remove a contrapartida nem recalcula o saldo"
// (DesfazerClassificacaoAsync, antes desta rodada, nunca apagava a contrapartida provisória — ver
// TransferenciaService). Esta rotina limpa o estrago já feito em dados existentes: contrapartidas
// que foram criadas do zero (Provisoria=true) mas cuja Transferência já foi desfeita ou nunca mais
// existiu (TransferenciaId=null) — um lançamento fantasma que nunca aconteceu de verdade no banco,
// "Sem categoria", com o saldo do dia ainda contando o efeito dele. Mesmo padrão dry-run-por-padrão
// do MigracaoRecorrenciasService/ReclassificacaoCategoriasService: sem confirmar=true, só relatório.
public class LimpezaTransferenciasService : ILimpezaTransferenciasService
{
    private readonly IRegistroRepository _registroRepo;
    private readonly IContaBancariaRepository _contaRepo;

    public LimpezaTransferenciasService(IRegistroRepository registroRepo, IContaBancariaRepository contaRepo)
    {
        _registroRepo = registroRepo;
        _contaRepo = contaRepo;
    }

    public async Task<LimpezaContrapartidasOrfasResultDto> LimparContrapartidasOrfasAsync(bool confirmar)
    {
        var resultado = new LimpezaContrapartidasOrfasResultDto { Confirmado = confirmar };
        var registros = await _registroRepo.ListarTodosNaoExcluidosAsync();
        var nomesContas = new Dictionary<Guid, string>();
        var contasAfetadas = new HashSet<Guid>();

        foreach (var registro in registros)
        {
            if (registro.ContaBancariaId is not { } contaId) continue;

            var saidasOrfas = registro.Saidas.Where(EhOrfa).ToList();
            var entradasOrfas = registro.Entradas.Where(EhOrfa).ToList();
            if (saidasOrfas.Count == 0 && entradasOrfas.Count == 0) continue;

            if (!nomesContas.TryGetValue(contaId, out var nomeConta))
            {
                nomeConta = (await _contaRepo.ObterPorIdAsync(contaId))?.Nome ?? "—";
                nomesContas[contaId] = nomeConta;
            }

            foreach (var s in saidasOrfas)
                resultado.Itens.Add(new ContrapartidaOrfaItemDto
                {
                    ClienteId = registro.ClienteId, ContaBancariaId = contaId, ContaBancariaNome = nomeConta,
                    Data = registro.Data, Descricao = s.Descricao, Valor = s.Valor, Tipo = "Saida",
                });
            foreach (var e in entradasOrfas)
                resultado.Itens.Add(new ContrapartidaOrfaItemDto
                {
                    ClienteId = registro.ClienteId, ContaBancariaId = contaId, ContaBancariaNome = nomeConta,
                    Data = registro.Data, Descricao = e.Descricao, Valor = e.Valor, Tipo = "Entrada",
                });

            if (confirmar)
            {
                registro.Saidas = registro.Saidas.Where(s => !EhOrfa(s)).ToList();
                registro.Entradas = registro.Entradas.Where(e => !EhOrfa(e)).ToList();
                registro.SalvoEm = DateTime.UtcNow;
                await _registroRepo.AtualizarAsync(registro);
                contasAfetadas.Add(contaId);
            }
        }

        resultado.ItensEncontrados = resultado.Itens.Count;
        if (!confirmar) return resultado;

        // Remover um item solto no meio do histórico não dá pra corrigir só com uma cascata de
        // delta a partir de um ponto — pode haver mais de uma data afetada, não necessariamente
        // contígua, na mesma conta (era exatamente esse o segundo sintoma do bug: saldo arrastando
        // uma divergência antiga, tipo o par "Guardado/Resgatado do RDB"). Mais seguro reconstruir a
        // cadeia inteira da conta do zero: Inicio do primeiro dia fica como está (é a âncora — ou
        // vem do saldo inicial da conta, ou já estava correto antes da varredura), cada dia seguinte
        // recebe Inicio = SaldoFinal do dia anterior, e SaldoFinal = Inicio + Σentradas − Σsaídas.
        foreach (var contaId in contasAfetadas)
        {
            var registrosDaConta = (await _registroRepo.ListarPorContaAsync(contaId)).OrderBy(r => r.Data).ToList();
            decimal? saldoCorrente = null;
            foreach (var r in registrosDaConta)
            {
                if (saldoCorrente.HasValue) r.Inicio = saldoCorrente.Value;
                r.SaldoFinal = r.Inicio + r.Entradas.Sum(e => e.Valor) - r.Saidas.Sum(s => s.Valor);
                r.SalvoEm = DateTime.UtcNow;
                await _registroRepo.AtualizarAsync(r);
                saldoCorrente = r.SaldoFinal;
            }
            resultado.ContasRecalculadas++;
        }

        return resultado;
    }

    private static bool EhOrfa(ItemFinanceiro e) => e.Provisoria && e.TransferenciaId == null;
    private static bool EhOrfa(ItemFinanceiroSaida s) => s.Provisoria && s.TransferenciaId == null;
}
