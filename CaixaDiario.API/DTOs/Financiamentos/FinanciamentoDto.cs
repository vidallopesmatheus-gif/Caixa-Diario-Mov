namespace CaixaDiario.API.DTOs.Financiamentos;

public class FinanciamentoDto
{
    public Guid Id { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public decimal ValorBem { get; set; }
    public decimal ValorEntrada { get; set; }
    public decimal ValorFinanciado { get; set; }
    public int NumeroParcelas { get; set; }
    public decimal? ValorParcela { get; set; }
    public decimal? TaxaJurosMensal { get; set; }
    public string Sistema { get; set; } = string.Empty;
    public DateOnly DataPrimeiraParcela { get; set; }
    public Guid ContaBancariaId { get; set; }
    public string ContaBancariaNome { get; set; } = string.Empty;
    public string? BemVinculado { get; set; }
    public string Status { get; set; } = string.Empty;

    // ── Fase 6.6: estatísticas ao vivo, derivadas das parcelas (nunca duplicadas em outro lugar) ──
    public decimal SaldoDevedor { get; set; }
    public int TotalParcelasAtual { get; set; }
    public int ParcelasPagas { get; set; }
    public decimal PercentualParcelasPagas { get; set; }
    public decimal PercentualDividaQuitada { get; set; }
    public decimal JurosPagos { get; set; }
    public decimal JurosAPagar { get; set; }
    public decimal JurosTotais { get; set; }
    public decimal? ProximaParcelaValor { get; set; }
    public DateOnly? ProximaParcelaVencimento { get; set; }
    public DateOnly? DataPrevistaQuitacao { get; set; }
    public decimal ValorAdiantado { get; set; }
    public decimal EconomiaJurosAcumulada { get; set; }
}
