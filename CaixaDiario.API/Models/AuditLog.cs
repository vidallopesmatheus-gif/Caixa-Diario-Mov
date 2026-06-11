namespace CaixaDiario.API.Models;

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsuarioId { get; set; }
    public string Acao { get; set; } = string.Empty;     // "CREATE" | "UPDATE" | "DELETE"
    public string Entidade { get; set; } = string.Empty;  // e.g. "RegistroDiario"
    public string? EntidadeId { get; set; }
    public string? Detalhes { get; set; }                 // JSON string, optional
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public Usuario Usuario { get; set; } = null!;
}
