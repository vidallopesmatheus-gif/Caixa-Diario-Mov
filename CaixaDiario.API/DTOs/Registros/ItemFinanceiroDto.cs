namespace CaixaDiario.API.DTOs.Registros;

public class ItemFinanceiroDto
{
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string? Categoria { get; set; }
    public string? TipoCusto { get; set; }
    public Guid? TransferenciaId { get; set; }
    public Guid Id { get; set; }
    public string? FitId { get; set; }
    public bool PendenteCategorizacao { get; set; }
}
