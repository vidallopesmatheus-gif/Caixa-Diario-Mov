using CaixaDiario.API.DTOs.Admin;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

// Money Vision: "saldo das contas não fecha com o extrato" — migração de reparo pra produção.
// RegistroService.RecalcularContaInteiraAsync já passou a ser chamado em todo write path que
// mexe em saldo (ver TransferenciaService/FaturaCartaoService/ContaBancariaService/
// ImportacaoService), então a cadeia nunca mais deveria desalinhar de novo — mas as contas que já
// existiam antes desta rodada continuam com a corrupção histórica até alguém rodar isto aqui.
// Mesmo padrão dry-run-por-padrão do MigracaoRecorrenciasService/LimpezaTransferenciasService: sem
// confirmar=true, só relatório, nada é gravado.
public class RecalculoSaldoService : IRecalculoSaldoService
{
    private readonly IContaBancariaRepository _contaRepo;
    private readonly IRegistroRepository _registroRepo;

    public RecalculoSaldoService(IContaBancariaRepository contaRepo, IRegistroRepository registroRepo)
    {
        _contaRepo = contaRepo;
        _registroRepo = registroRepo;
    }

    public async Task<RecalculoSaldoResultDto> RecalcularTodasAsContasAsync(bool confirmar)
    {
        var resultado = new RecalculoSaldoResultDto { Confirmado = confirmar };
        var contas = await _contaRepo.ListarTodasAsync();

        foreach (var conta in contas)
        {
            var registros = (await _registroRepo.ListarPorContaAsync(conta.Id))
                .Where(r => !r.Excluido)
                .OrderBy(r => r.Data)
                .ToList();
            if (registros.Count == 0) continue;

            var saldoAtualAntes = registros[^1].SaldoFinal;
            var saldoCorrente = conta.SaldoInicial;
            DateOnly? primeiraDivergencia = null;

            foreach (var registro in registros)
            {
                var deltaDoDia = registro.SaldoFinal - registro.Inicio;
                if (registro.Inicio != saldoCorrente)
                {
                    primeiraDivergencia ??= registro.Data;
                    if (confirmar)
                    {
                        registro.Inicio = saldoCorrente;
                        registro.SaldoFinal = saldoCorrente + deltaDoDia;
                        await _registroRepo.AtualizarAsync(registro);
                    }
                }
                // Sem confirmar: nunca grava, só segue a matemática em memória pra saber qual
                // seria o saldo final correto, pro relatório.
                saldoCorrente = confirmar ? registro.SaldoFinal : saldoCorrente + deltaDoDia;
            }

            if (primeiraDivergencia.HasValue)
            {
                resultado.Itens.Add(new DivergenciaSaldoContaDto
                {
                    ClienteId = conta.ClienteId,
                    ContaBancariaId = conta.Id,
                    ContaBancariaNome = conta.Nome,
                    PrimeiraDataDivergente = primeiraDivergencia.Value,
                    SaldoAtualAntes = saldoAtualAntes,
                    SaldoAtualDepois = saldoCorrente,
                });
            }
        }

        resultado.ContasComDivergencia = resultado.Itens.Count;
        return resultado;
    }
}
