using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface IMetaRepository
{
    // Escopado ao modo "simples" — é o único modo em que (ClienteId, Ano) ainda é uma identidade
    // válida (índice único parcial no banco). Objetivos (modo "metodo") são buscados por Id.
    Task<MetaAnual?> ObterMetaSimplesPorClienteEAnoAsync(Guid clienteId, int ano);
    Task<MetaAnual?> ObterPorIdAsync(Guid id);
    Task<List<MetaAnual>> ListarPorClienteAsync(Guid clienteId);
    Task<MetaAnual> SalvarAsync(MetaAnual meta);
    Task RemoverAsync(MetaAnual meta);
}
