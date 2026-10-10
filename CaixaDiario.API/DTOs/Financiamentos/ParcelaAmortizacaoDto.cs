namespace CaixaDiario.API.DTOs.Financiamentos;

/// <summary>Uma linha da tabela de amortização (Fase 6.3) — nunca persistida como está; cada linha
/// vira uma ContaProvisionada com OrigemFinanciamentoId + a decomposição Juros/Amortizacao.</summary>
public class ParcelaAmortizacaoDto
{
    public int Numero { get; set; }
    public DateOnly Vencimento { get; set; }
    public decimal Valor { get; set; }
    public decimal Juros { get; set; }
    public decimal Amortizacao { get; set; }
    public decimal SaldoDevedor { get; set; }
}
