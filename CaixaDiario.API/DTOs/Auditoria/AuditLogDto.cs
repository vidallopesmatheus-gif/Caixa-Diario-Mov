namespace CaixaDiario.API.DTOs.Auditoria;

public class AuditLogDto
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public Guid UsuarioId { get; set; }
    public string Entidade { get; set; } = string.Empty;
    public string AcaoTipo { get; set; } = string.Empty;
    public string EntidadeId { get; set; } = string.Empty;
    public string? DadosAntes { get; set; }
    public string? DadosDepois { get; set; }
    public DateTime OcorridoEm { get; set; }
}

public class AuditLogPaginadoDto
{
    public List<AuditLogDto> Items { get; set; } = new();
    public int Total { get; set; }
    public int Pagina { get; set; }
    public int TamanhoPagina { get; set; }
}
