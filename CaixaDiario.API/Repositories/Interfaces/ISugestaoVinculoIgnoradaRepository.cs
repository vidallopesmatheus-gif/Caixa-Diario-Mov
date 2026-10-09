using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface ISugestaoVinculoIgnoradaRepository
{
    Task<List<SugestaoVinculoIgnorada>> ListarPorClienteAsync(Guid clienteId);
    Task AdicionarAsync(SugestaoVinculoIgnorada ignorada);
}
