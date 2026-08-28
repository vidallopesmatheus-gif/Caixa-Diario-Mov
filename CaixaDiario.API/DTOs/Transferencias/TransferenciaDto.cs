namespace CaixaDiario.API.DTOs.Transferencias;

public class TransferenciaDto
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public Guid ContaOrigemId { get; set; }
    public string ContaOrigemNome { get; set; } = string.Empty;
    public Guid ContaDestinoId { get; set; }
    public string ContaDestinoNome { get; set; } = string.Empty;
    public DateOnly Data { get; set; }
    public decimal Valor { get; set; }
    public string? Descricao { get; set; }
    public DateTime CriadoEm { get; set; }
}
