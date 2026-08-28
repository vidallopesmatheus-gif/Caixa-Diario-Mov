using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface ITransferenciaRepository
{
    Task<Transferencia?> ObterPorIdAsync(Guid id);
    Task<List<Transferencia>> ListarPorClienteAsync(Guid clienteId);
    Task<Transferencia> AdicionarAsync(Transferencia transferencia);
    Task RemoverAsync(Transferencia transferencia);
}
