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

    public static bool EhOperacional(string? tipoCusto) =>
        tipoCusto != TipoTransferencia && tipoCusto != TipoRendimento;
}
