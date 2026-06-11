using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface IContaRecorrenteRepository
{
    Task<List<ContaRecorrente>> GetByUsuarioIdAsync(Guid usuarioId);
    Task<ContaRecorrente?> GetByIdAsync(Guid id);
    Task<ContaRecorrente> CreateAsync(ContaRecorrente conta);
    Task<ContaRecorrente> UpdateAsync(ContaRecorrente conta);
    Task DeleteAsync(Guid id);
}
