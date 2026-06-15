namespace CaixaDiario.API.DTOs.Metricas;

public class EvolucaoMensalDto
{
    public string Mes { get; set; } = string.Empty;
    public decimal Receita { get; set; }
    public decimal Custos { get; set; }
    public decimal Lucro { get; set; }
    public decimal Saldo { get; set; }
}
