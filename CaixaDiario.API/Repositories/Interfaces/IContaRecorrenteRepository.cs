using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface IContaRecorrenteRepository
{
    Task<List<ContaRecorrente>> ListarAtivasPorClienteAsync(Guid clienteId);
    Task<ContaRecorrente?> ObterPorIdAsync(Guid clienteId, Guid id);
    Task<ContaRecorrente> AdicionarAsync(ContaRecorrente conta);
    Task<ContaRecorrente> AtualizarAsync(ContaRecorrente conta);
}
