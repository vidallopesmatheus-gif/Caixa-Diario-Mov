namespace CaixaDiario.API.DTOs.FaturasCartao;

public class FaturaCartaoDto
{
    // "yyyy-MM" — identifica a fatura pro resto do fluxo (ver PagamentoFatura.Competencia).
    public string Competencia { get; set; } = string.Empty;
    public DateOnly DataFechamento { get; set; }
    public DateOnly DataVencimento { get; set; }
    public decimal ValorTotal { get; set; }
    public decimal ValorPago { get; set; }
    public decimal SaldoDevedor { get; set; }
    public int QuantidadeCompras { get; set; }
    // "Aberta" (ciclo ainda não fechou) | "Fechada" (fechou, saldo pendente) | "Paga"
    public string Status { get; set; } = string.Empty;
}
