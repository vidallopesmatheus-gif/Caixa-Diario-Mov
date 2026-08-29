namespace CaixaDiario.API.DTOs.ContasBancarias;

public class LancamentoExtratoDto
{
    // Nulo pras linhas sintéticas de recebimento/pagamento de Contas a Pagar/Receber (não são um
    // ItemFinanceiro/ItemFinanceiroSaida de verdade) — só entradas/saídas reais têm Id pra reclassificar.
    public Guid? Id { get; set; }
    public string Data { get; set; } = string.Empty; // "yyyy-MM-dd"
    public string Descricao { get; set; } = string.Empty;
    public string? Categoria { get; set; }
    public decimal Valor { get; set; } // positivo = entrada, negativo = saída
    public decimal SaldoAcumulado { get; set; }
    public bool PendenteCategorizacao { get; set; }
}
