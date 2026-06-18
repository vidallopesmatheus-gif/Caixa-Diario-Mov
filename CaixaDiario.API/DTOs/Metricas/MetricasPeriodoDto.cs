namespace CaixaDiario.API.DTOs.Metricas;

public class MetricasPeriodoDto
{
    public EbitdaDto? Ebitda { get; set; }
    public PrimeCostDto? PrimeCost { get; set; }
    public PontoDeEquilibrioDto? PontoDeEquilibrio { get; set; }
    public decimal SaldoProjetado { get; set; }
    public decimal? BurnRate { get; set; }
    public ValuationDto? Valuation { get; set; }
    public RunwayDto? Runway { get; set; }
    public LiquidezDto? Liquidez { get; set; }
    public TicketMedioDto? TicketMedio { get; set; }
}

public class TicketMedioDto
{
    public decimal Valor { get; set; }
    public int QuantidadeRecebimentos { get; set; }
}

public class EbitdaDto
{
    public decimal Valor { get; set; }
    public decimal? Percentual { get; set; }
    public string Semaforo { get; set; } = "cinza";
}

public class PrimeCostDto
{
    public decimal? Percentual { get; set; }
    public string Semaforo { get; set; } = "cinza";
}

public class PontoDeEquilibrioDto
{
    public decimal Valor { get; set; }
    public decimal Receita { get; set; }
    public string Semaforo { get; set; } = "cinza";
}

public class ValuationDto
{
    public decimal Valor { get; set; }
    public string Semaforo { get; set; } = "cinza";
}

public class RunwayDto
{
    public decimal Meses { get; set; }
    public string Semaforo { get; set; } = "cinza";
}

public class LiquidezDto
{
    public decimal? Indice { get; set; }
    public bool AltaLiquidez { get; set; }
    public string Semaforo { get; set; } = "cinza";
}
