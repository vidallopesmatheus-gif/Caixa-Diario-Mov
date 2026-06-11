namespace CaixaDiario.API.Models;

public class AuditLog
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public string Acao { get; set; } = string.Empty;
    public string Entidade { get; set; } = string.Empty;
    public string? EntidadeId { get; set; }
    public string? Detalhes { get; set; }                 // JSON string, optional
    public DateTime CriadoEm { get; set; }

    public Usuario Usuario { get; set; } = null!;
}
