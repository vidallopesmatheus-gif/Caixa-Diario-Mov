using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface ISugestaoRecorrenciaIgnoradaRepository
{
    Task<List<SugestaoRecorrenciaIgnorada>> ListarPorClienteAsync(Guid clienteId);
    Task AdicionarAsync(SugestaoRecorrenciaIgnorada ignorada);
}
