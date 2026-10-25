namespace CaixaDiario.API.Models;

// Money Vision: parcelamentos — agrupa as parcelas de uma compra parcelada no cartão (detectada na
// importação da fatura, ou lançada manualmente via FaturaCartaoService.LancarComprasParceladaAsync)
// num registro só, com status e vínculo de meta. As parcelas em si continuam vivendo nos lugares de
// sempre: ItemFinanceiroSaida (parcela já real, confirmada por uma fatura importada ou lançada
// manualmente) e ContaProvisionada em Contas a Pagar (parcela futura, ainda só prevista — criada na
// detecção, até a fatura real daquele mês chegar e a parcela virar real) — ambas carregam
// ParcelamentoId + NumeroParcela em comum.
public class Parcelamento
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public Guid ContaCartaoId { get; set; }
    public string DescricaoBase { get; set; } = string.Empty;
    public string? Categoria { get; set; }
    public decimal ValorParcela { get; set; }
    public int TotalParcelas { get; set; }
    // Vencimento inferido da parcela 1 — quando o parcelamento é descoberto a partir de uma parcela
    // N > 1 (parcelas 1..N-1 já aconteceram antes de existir a detecção), é calculado pra trás
    // (DataVencimento da parcela atual menos (N-1) meses), nunca observado de verdade.
    public DateOnly DataPrimeira { get; set; }
    public decimal ValorTotal { get; set; }
    public Guid? MetaId { get; set; }
    public string Status { get; set; } = "Ativo"; // "Ativo" | "Quitado"
    public DateTime CriadoEm { get; set; }
}
