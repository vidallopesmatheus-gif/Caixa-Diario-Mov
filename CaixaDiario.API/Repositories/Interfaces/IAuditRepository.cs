using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface IAuditRepository
{
    Task<(List<AuditLog> items, int total)> ListarPaginadoAsync(
        Guid clienteId,
        DateTime? de,
        DateTime? ate,
        string? entidade,
        string? acaoTipo,
        int pagina,
        int tamanhoPagina);
}
