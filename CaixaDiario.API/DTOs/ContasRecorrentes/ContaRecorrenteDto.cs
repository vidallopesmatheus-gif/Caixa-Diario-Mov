namespace CaixaDiario.API.DTOs.ContasRecorrentes;

public class ContaRecorrenteDto
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string? Categoria { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public DateOnly DataInicio { get; set; }
    public DateOnly? DataFim { get; set; }
    public bool Ativo { get; set; }
    public DateTime CriadoEm { get; set; }
}
