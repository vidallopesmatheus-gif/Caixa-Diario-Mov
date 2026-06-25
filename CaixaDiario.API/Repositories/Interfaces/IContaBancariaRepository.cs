using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface IContaBancariaRepository
{
    Task<List<ContaBancaria>> ListarPorClienteAsync(Guid clienteId);
    Task<ContaBancaria?> ObterPorIdAsync(Guid id);
    Task<ContaBancaria?> ObterCaixaPadraoAsync(Guid clienteId);
    Task<ContaBancaria> AdicionarAsync(ContaBancaria conta);
    Task<ContaBancaria> AtualizarAsync(ContaBancaria conta);
}
