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
    // Preenchido só quando Categoria == "Transferência" — é o id do registro de Transferencia (não
    // do lançamento), usado pra desfazer a classificação (DELETE /api/transferencias/{id}).
    public Guid? TransferenciaId { get; set; }
    // Preenchido quando a categoria veio de uma RegraCategorizacao (importação automática ou
    // aplicação retroativa) — corrigir manualmente não limpa isso, só marca a origem pra exibição.
    public Guid? RegraCategorizacaoId { get; set; }
}
