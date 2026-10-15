namespace CaixaDiario.API.DTOs.Metricas;

/// <summary>
/// Money Vision — Bloco 5, item 2 da Parte 3: explica o fluxo do mês em 4 passos (card "Para onde
/// foi meu dinheiro", no topo do Banco e no Dashboard) — por que o saldo em conta corrente está
/// baixo mesmo com receita boa, quando a diferença foi guardada em investimento.
/// </summary>
public class ParaOndeFoiMeuDinheiroDto
{
    public decimal Entrou { get; set; }
    public decimal Gastou { get; set; }
    /// <summary>Aplicações menos resgates em contas de investimento no período — nunca negativo nem positivo por si só.</summary>
    public decimal Guardou { get; set; }
    /// <summary>O que sobrou em conta corrente: Entrou - Gastou - Guardou.</summary>
    public decimal Ficou { get; set; }
    /// <summary>Guardou / Entrou — null quando não há receita no período (divisão indefinida).</summary>
    public decimal? TaxaPoupancaPercentual { get; set; }
    /// <summary>Redesign visual (Money Vision): as 3 maiores categorias de saída do período, pro
    /// fluxo "Entrou → Gastou (top 3) → Guardou → Ficou" — só apresentação, nunca mudam Gastou.</summary>
    public List<DreCategoriaDto> TopCategoriasGasto { get; set; } = new();
}
