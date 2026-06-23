using CaixaDiario.API.Data;
using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

public class AuditService : IAuditService
{
    private readonly AppDbContext _context;

    public AuditService(AppDbContext context) => _context = context;

    public async Task LogAsync(
        Guid clienteId,
        Guid usuarioId,
        string entidade,
        string acaoTipo,
        string entidadeId,
        string? dadosAntes,
        string? dadosDepois)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            ClienteId = clienteId,
            UsuarioId = usuarioId,
            Entidade = entidade,
            AcaoTipo = acaoTipo,
            EntidadeId = entidadeId,
            DadosAntes = dadosAntes,
            DadosDepois = dadosDepois,
        });
        await _context.SaveChangesAsync();
    }
}
