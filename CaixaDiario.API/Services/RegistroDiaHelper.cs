using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

/// <summary>
/// Resolve-ou-cria o RegistroDiario de uma conta numa data, reaproveitado por
/// transferências e registro de rendimento (que só acrescentam 1 item ao dia,
/// diferente do fluxo completo de CriarRegistroDto usado no Caixa manual).
/// </summary>
public static class RegistroDiaHelper
{
    public static async Task<(RegistroDiario registro, bool novo)> ResolverOuCriarAsync(
        IRegistroRepository registroRepo, ContaBancaria conta, DateOnly data)
    {
        var existente = await registroRepo.ObterPorContaEDataAsync(conta.Id, data);
        if (existente != null && !existente.Excluido)
            return (existente, false);

        var registrosDaConta = await registroRepo.ListarPorContaAsync(conta.Id);
        var anterior = registrosDaConta
            .Where(r => !r.Excluido && r.Data < data)
            .OrderByDescending(r => r.Data)
            .FirstOrDefault();
        var saldoBase = anterior?.SaldoFinal ?? conta.SaldoInicial;

        var novo = new RegistroDiario
        {
            Id = Guid.NewGuid(),
            ClienteId = conta.ClienteId,
            ContaBancariaId = conta.Id,
            Data = data,
            Inicio = saldoBase,
            Entradas = new(),
            Saidas = new(),
            ContasReceber = new(),
            ContasPagar = new(),
            SaldoFinal = saldoBase,
            CriadoEm = DateTime.UtcNow,
            SalvoEm = DateTime.UtcNow,
        };
        return (novo, true);
    }

    public static Task PersistirAsync(IRegistroRepository registroRepo, RegistroDiario registro, bool novo) =>
        novo ? registroRepo.AdicionarAsync(registro) : registroRepo.AtualizarAsync(registro);
}
