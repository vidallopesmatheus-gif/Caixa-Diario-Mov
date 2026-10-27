using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface IContaBancariaRepository
{
    Task<List<ContaBancaria>> ListarPorClienteAsync(Guid clienteId);
    // Varredura administrativa entre clientes (ver RecalculoSaldoService) — nunca usado em rota de
    // cliente comum, só em ferramenta de admin.
    Task<List<ContaBancaria>> ListarTodasAsync();
    Task<ContaBancaria?> ObterPorIdAsync(Guid id);
    Task<ContaBancaria> AdicionarAsync(ContaBancaria conta);
    Task<ContaBancaria> AtualizarAsync(ContaBancaria conta);
    Task RemoverAsync(ContaBancaria conta);
}
