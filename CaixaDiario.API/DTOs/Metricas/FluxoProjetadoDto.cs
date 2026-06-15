namespace CaixaDiario.API.DTOs.Metricas;

public class FluxoProjetadoDto
{
    public decimal SaldoAtual { get; set; }
    public List<FluxoDiaDto> Dias { get; set; } = new();
}

public class FluxoDiaDto
{
    public DateOnly Data { get; set; }
    public decimal SaldoProjetado { get; set; }
}
