namespace CaixaDiario.API.DTOs.Insights;

public class InsightDto
{
    public string Tipo { get; set; } = "neutro"; // "alerta" | "positivo" | "neutro"
    public string Texto { get; set; } = "";
    public string? Detalhe { get; set; }
    public int Prioridade { get; set; }
}
