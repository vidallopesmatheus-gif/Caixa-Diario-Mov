namespace CaixaDiario.API.Services;

public interface IAuditService
{
    Task LogAsync(
        Guid clienteId,
        Guid usuarioId,
        string entidade,
        string acaoTipo,
        string entidadeId,
        string? dadosAntes,
        string? dadosDepois);
}
