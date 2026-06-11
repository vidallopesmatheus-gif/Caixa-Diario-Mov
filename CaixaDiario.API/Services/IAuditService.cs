namespace CaixaDiario.API.Services;

public interface IAuditService
{
    Task LogAsync(Guid usuarioId, string acao, string entidade, string? entidadeId = null, string? detalhes = null);
}
