using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface IMetaRepository
{
    Task<MetaAnual?> ObterPorClienteEAnoAsync(Guid clienteId, int ano);
    Task<MetaAnual> SalvarAsync(MetaAnual meta);
}
