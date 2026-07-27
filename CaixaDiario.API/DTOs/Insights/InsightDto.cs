namespace CaixaDiario.API.DTOs.Insights;

public class InsightDto
{
    public string Tipo { get; set; } = "neutro"; // "alerta" | "positivo" | "neutro"
    public string Texto { get; set; } = "";
    public string? Detalhe { get; set; }
    public int Prioridade { get; set; }
    // "saldo" | "gasto" | "meta" | "lucro" — usado pelo front para linkar o insight à tela onde ele se resolve.
    public string Categoria { get; set; } = "geral";
}
