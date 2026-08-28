namespace CaixaDiario.API.Models;

public class Categoria
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    // "Receita" | "CustoVariavel" | "CustoFixo" | "DespesaNaoOperacional"
    public string Tipo { get; set; } = string.Empty;
    // Bucket de exibição do DRE (ex.: "Custos Diretos", "Pessoas"). Nulo → cai em "Outros".
    public string? Grupo { get; set; }
    public int Ordem { get; set; }
    public bool Ativa { get; set; } = true;
    public DateTime CriadoEm { get; set; }
}
