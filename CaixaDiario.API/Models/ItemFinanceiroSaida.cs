namespace CaixaDiario.API.Models;

public class ItemFinanceiroSaida
{
    public Guid Id { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string Categoria { get; set; } = "Administrativas";
    public decimal Valor { get; set; }
    public string? Subcategoria { get; set; }
    public string? TipoCusto { get; set; }
    // Preenchido apenas quando TipoCusto == "Transferencia": liga as duas pontas do par.
    public Guid? TransferenciaId { get; set; }
    // Preenchido apenas quando TipoCusto == "PagamentoFatura": esta saída (na conta corrente) é o
    // pagamento de uma fatura de cartão, não uma despesa nova — ver FaturaCartaoService.
    public Guid? PagamentoFaturaId { get; set; }
    // Identificador único do OFX (dedup) — nulo para lançamentos manuais ou vindos de CSV/XLSX.
    public string? FitId { get; set; }
    // Verdadeiro quando a importação não encontrou categoria sugerida — some quando o usuário categoriza.
    public bool PendenteCategorizacao { get; set; }
    // Item 3.4: Categoria já vem preenchida (histórico do cliente ou dicionário padrão), mas
    // AINDA precisa de confirmação — diferente de uma regra do cliente, que confirma sozinha.
    // Sempre false quando PendenteCategorizacao é false (nunca os dois juntos).
    public bool CategoriaSugerida { get; set; }
    // Preenchido quando a categoria veio de uma RegraCategorizacao aplicada na importação —
    // corrigir manualmente NÃO limpa isso nem altera a regra (só marca a origem pra exibição).
    public Guid? RegraCategorizacaoId { get; set; }
    // Verdadeiro quando quem escolheu a categoria foi o próprio cliente, pelo portal de
    // conciliação (link público) — nunca setado quando o consultor categoriza pela tela normal.
    public bool ClassificadoPeloCliente { get; set; }
    // Verdadeiro só pra contrapartida de Transferência CRIADA automaticamente (TransferenciaService.
    // ConverterLancamentoAsync) antes do extrato real da conta de destino ter sido importado — ainda
    // não foi confirmada pelo banco. A importação tenta casar essa provisória com a transação real
    // (mesmo valor, mesmo sentido, ±2 dias úteis) em vez de duplicar — ver ImportacaoService.
    public bool Provisoria { get; set; }
}
