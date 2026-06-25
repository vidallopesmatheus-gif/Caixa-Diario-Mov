using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface IMetaRepository
{
    Task<MetaAnual?> ObterPorClienteEAnoAsync(Guid clienteId, int ano);
    Task<List<MetaAnual>> ListarPorClienteAsync(Guid clienteId);
    Task<MetaAnual> SalvarAsync(MetaAnual meta);
}
