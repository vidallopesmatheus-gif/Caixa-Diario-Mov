namespace CaixaDiario.API.Services;

/// <summary>
/// Regra única de exclusão de lançamentos "não operacionais" (transferência entre contas,
/// rendimento de investimento) das métricas de resultado (DRE, Indicadores, Orçamento
/// Dinâmico, Saúde Financeira). Esses lançamentos são reais no extrato/Caixa — só não
/// contam como receita/despesa de negócio.
/// </summary>
public static class LancamentoFiltro
{
    public const string TipoTransferencia = "Transferencia";
    public const string TipoRendimento = "Rendimento";
    // Mesmo tratamento de Rendimento: entram no DRE só depois do Resultado Operacional (Atividades
    // de Investimento/Financiamento), nunca em Ebitda, ponto de equilíbrio, evolução de receita/custo etc.
    public const string TipoInvestimento = "Investimento";
    public const string TipoFinanciamento = "Financiamento";

    public static bool EhOperacional(string? tipoCusto) =>
        tipoCusto != TipoTransferencia && tipoCusto != TipoRendimento
        && tipoCusto != TipoInvestimento && tipoCusto != TipoFinanciamento;
}
