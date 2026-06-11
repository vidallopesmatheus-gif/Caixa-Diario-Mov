namespace CaixaDiario.API.Services;

using CaixaDiario.API.Data;
using CaixaDiario.API.Models;

public class AuditService : IAuditService
{
    private readonly AppDbContext _context;

    public AuditService(AppDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(Guid usuarioId, string acao, string entidade, string? entidadeId = null, string? detalhes = null)
    {
        var log = new AuditLog
        {
            UsuarioId = usuarioId,
            Acao = acao,
            Entidade = entidade,
            EntidadeId = entidadeId,
            Detalhes = detalhes
        };
        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync();
    }
}
