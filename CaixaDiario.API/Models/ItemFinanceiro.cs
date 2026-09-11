namespace CaixaDiario.API.Models;

public class ItemFinanceiro
{
    // Guid.Empty para lançamentos manuais antigos (nunca precisaram de id estável).
    // Itens importados sempre recebem um Id novo, usado para localizar o item na
    // categorização posterior sem precisar de outra tabela.
    public Guid Id { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string? Categoria { get; set; }
    public string? TipoCusto { get; set; }
    // Preenchido apenas quando TipoCusto == "Transferencia": liga as duas pontas do par.
    public Guid? TransferenciaId { get; set; }
    // Identificador único do OFX (dedup) — nulo para lançamentos manuais ou vindos de CSV/XLSX.
    public string? FitId { get; set; }
    // Verdadeiro para toda entrada importada (nunca tem sugestão automática de categoria —
    // só o usuário sabe distinguir receita real de transferência/resgate).
    public bool PendenteCategorizacao { get; set; }
    // Preenchido quando a categoria veio de uma RegraCategorizacao aplicada na importação —
    // corrigir manualmente NÃO limpa isso nem altera a regra (só marca a origem pra exibição).
    public Guid? RegraCategorizacaoId { get; set; }
    // Verdadeiro quando quem escolheu a categoria foi o próprio cliente, pelo portal de
    // conciliação (link público) — nunca setado quando o consultor categoriza pela tela normal.
    public bool ClassificadoPeloCliente { get; set; }
}
