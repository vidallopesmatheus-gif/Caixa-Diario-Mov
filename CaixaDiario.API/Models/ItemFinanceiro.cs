namespace CaixaDiario.API.Models;

public class ItemFinanceiro
{
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string? Categoria { get; set; }
    public string? TipoCusto { get; set; }  // "fixo" | "variavel" | null
}
