namespace CaixaDiario.API.DTOs.Financiamentos;

public class BaixarParcelaFinanciamentoDto
{
    public Guid ParcelaId { get; set; }
    public DateOnly DataPagamento { get; set; }
    // Se informado e maior que o valor da parcela, a diferença entra junto com os juros
    // (atraso/multa) — ver FinanciamentoService.BaixarParcelaAsync.
    public decimal? ValorPago { get; set; }
}
