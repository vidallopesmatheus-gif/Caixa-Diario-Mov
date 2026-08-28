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
    // Verdadeiro quando a importação não encontrou categoria sugerida — só usado no lado das
    // saídas hoje (ver ItemFinanceiroSaida), mantido aqui por simetria/uso futuro.
    public bool PendenteCategorizacao { get; set; }
}
