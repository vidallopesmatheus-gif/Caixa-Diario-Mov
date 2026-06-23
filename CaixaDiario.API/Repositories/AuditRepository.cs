using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Repositories;

public class AuditRepository : IAuditRepository
{
    private readonly AppDbContext _context;

    public AuditRepository(AppDbContext context) => _context = context;

    public async Task<(List<AuditLog> items, int total)> ListarPaginadoAsync(
        Guid clienteId,
        DateTime? de,
        DateTime? ate,
        string? entidade,
        string? acaoTipo,
        int pagina,
        int tamanhoPagina)
    {
        var query = _context.AuditLogs
            .Where(l => l.ClienteId == clienteId)
            .AsQueryable();

        if (de.HasValue) query = query.Where(l => l.OcorridoEm >= de.Value);
        if (ate.HasValue) query = query.Where(l => l.OcorridoEm <= ate.Value);
        if (!string.IsNullOrWhiteSpace(entidade)) query = query.Where(l => l.Entidade == entidade);
        if (!string.IsNullOrWhiteSpace(acaoTipo)) query = query.Where(l => l.AcaoTipo == acaoTipo);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(l => l.OcorridoEm)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync();

        return (items, total);
    }
}
