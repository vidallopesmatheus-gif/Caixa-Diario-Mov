namespace CaixaDiario.API.DTOs.Financiamentos;

/// <summary>Uma linha da tabela de amortização "ao vivo" (a própria ContaProvisionada da parcela).</summary>
public class ParcelaFinanciamentoDto
{
    public Guid Id { get; set; }
    public int Numero { get; set; }
    public DateOnly Vencimento { get; set; }
    public decimal Valor { get; set; }
    public decimal Juros { get; set; }
    public decimal Amortizacao { get; set; }
    public decimal Encargos { get; set; }
    public decimal SaldoDevedor { get; set; }
    public bool Pago { get; set; }
    public DateOnly? DataBaixa { get; set; }
    public bool Atrasada { get; set; }
}
