namespace CaixaDiario.API.Models;

public class AuditLog
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public Guid UsuarioId { get; set; }
    public string Entidade { get; set; } = string.Empty;
    public string AcaoTipo { get; set; } = string.Empty;  // "Criacao" | "Edicao" | "Exclusao"
    public string EntidadeId { get; set; } = string.Empty;
    public string? DadosAntes { get; set; }
    public string? DadosDepois { get; set; }
    public DateTime OcorridoEm { get; set; }
}
