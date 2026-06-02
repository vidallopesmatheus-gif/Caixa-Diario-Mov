namespace CaixaDiario.API.DTOs.Registros;

public class ItemFinanceiroSaidaDto
{
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string Categoria { get; set; } = "Administrativas";
    public string? Subcategoria { get; set; }
}
