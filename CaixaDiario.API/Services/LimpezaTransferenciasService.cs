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
                // Diferente do merge de importação (onde um "ajuste" pré-existente é uma baixa
                // financeira legítima que tem que ser preservada): aqui o SaldoFinal armazenado pode
                // SER a própria corrupção que esta limpeza existe pra eliminar (ex.: o "R$400 a mais"
                // do relato). Por isso recomputa do zero (ajuste=0) em vez de preservar a diferença
                // que já havia — confiar de novo nela reintroduziria o mesmo bug que está sendo corrigido.
                registro.Saidas = registro.Saidas.Where(s => !EhOrfa(s)).ToList();
                registro.Entradas = registro.Entradas.Where(e => !EhOrfa(e)).ToList();
                registro.SaldoFinal = RegistroService.CalcularSaldoFinal(registro.Inicio, registro.Entradas, registro.Saidas, 0m);
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
        // uma divergência antiga, tipo o par "Guardado/Resgatado do RDB"). RecalcularContaInteiraAsync
        // (fonte única de verdade, Money Vision "saldo não fecha com o extrato") reconstrói a cadeia
        // de Inicio de toda a conta a partir do saldo inicial, preservando o SaldoFinal local que
        // cada dia já tem (já corrigido acima, incluindo o ajuste).
        foreach (var contaId in contasAfetadas)
        {
            var conta = await _contaRepo.ObterPorIdAsync(contaId);
            if (conta == null) continue;
            await RegistroService.RecalcularContaInteiraAsync(_registroRepo, contaId, conta.SaldoInicial);
            resultado.ContasRecalculadas++;
        }

        return resultado;
    }

    private static bool EhOrfa(ItemFinanceiro e) => e.Provisoria && e.TransferenciaId == null;
    private static bool EhOrfa(ItemFinanceiroSaida s) => s.Provisoria && s.TransferenciaId == null;
}
