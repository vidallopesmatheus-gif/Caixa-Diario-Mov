namespace CaixaDiario.API.Models;

public class ItemFinanceiroSaida
{
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string Categoria { get; set; } = "Administrativas";
    public string? Subcategoria { get; set; }
    public string? TipoCusto { get; set; }
}
