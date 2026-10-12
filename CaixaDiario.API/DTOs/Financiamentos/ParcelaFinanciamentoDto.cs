namespace CaixaDiario.API.DTOs.Financiamentos;

/// <summary>Uma linha da tabela de amortização "ao vivo" — ou uma parcela (a própria
/// ContaProvisionada), ou um evento de adiantamento (Tipo == "Adiantamento", sem Numero/Juros/
/// Encargos/Pago/DataBaixa/Atrasada; só Data/Valor/SaldoDevedor fazem sentido pra ela). Inserida
/// na posição cronológica certa pra o "Saldo devedor" de cada linha seguinte já saír descontado —
/// sem isso a última parcela nunca zerava (rodada "Visão de Futuro", item 1).</summary>
public class ParcelaFinanciamentoDto
{
    public Guid Id { get; set; }
    // "Parcela" | "Adiantamento"
    public string Tipo { get; set; } = "Parcela";
    public int? Numero { get; set; }
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
