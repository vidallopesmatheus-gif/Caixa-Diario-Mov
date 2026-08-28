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
    // Identificador único do OFX (dedup) — nulo para lançamentos manuais ou vindos de CSV/XLSX.
    public string? FitId { get; set; }
    // Verdadeiro quando a importação não encontrou categoria sugerida — some quando o usuário categoriza.
    public bool PendenteCategorizacao { get; set; }
}
