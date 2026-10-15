namespace CaixaDiario.API.DTOs.FaturasCartao;

public class FaturaCartaoDto
{
    // Id da Fatura real (Models/Fatura.cs). Null só pra compras sem FaturaId (parcelas lançadas
    // direto no cartão antes do Bloco 4) — essas continuam agrupadas pela competência inferida.
    public Guid? Id { get; set; }
    // Emissor (ex.: "Nubank", "Itaú") — null no grupo "virtual" (sem Fatura real).
    public string? Banco { get; set; }
    // "yyyy-MM" — identifica a fatura pro resto do fluxo (ver PagamentoFatura.Competencia).
    public string Competencia { get; set; } = string.Empty;
    public DateOnly DataFechamento { get; set; }
    public DateOnly DataVencimento { get; set; }
    public decimal ValorTotal { get; set; }
    public decimal ValorPago { get; set; }
    public decimal SaldoDevedor { get; set; }
    public int QuantidadeCompras { get; set; }
    // "Aberta" (ciclo ainda não fechou) | "Fechada" (fechou, nada pago) | "Parcial" (pago < total) | "Paga"
    public string Status { get; set; } = string.Empty;
}
