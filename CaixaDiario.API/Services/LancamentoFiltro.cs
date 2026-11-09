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
    // Pagamento de fatura de cartão (na conta corrente) e a baixa correspondente na conta do
    // cartão (ver FaturaCartaoService) — a despesa já foi reconhecida na compra, então o pagamento
    // da fatura não pode contar de novo, senão duplica no DRE.
    public const string TipoPagamentoFatura = "PagamentoFatura";
    // Money Vision — Bloco 3: linha sintética de acerto de saldo (ex.: descrição "saldo" lançada
    // manualmente pra bater o extrato do banco) — nunca é receita/despesa real, nunca entra no
    // DRE nem nas médias, mesmo que tenha sido categorizada (ex.: "Devoluções") antes desta correção.
    public const string TipoAjusteSaldo = "AjusteSaldo";
    // Money Vision ("ajuste mexeu no DRE"): tipo de sistema do ajuste de conciliação de saldo — é
    // correção de conciliação, nunca receita/despesa. "AjusteSaldo" é o valor legado, tratado igual
    // até a migração (RecalculoSaldoService.MigrarAjustesConciliacaoAsync) converter todos.
    public const string TipoAjusteConciliacao = "AjusteConciliacao";
    public const string CategoriaAjusteSaldo = "Ajuste de saldo";

    public static bool EhAjusteConciliacao(string? tipoCusto) =>
        tipoCusto == TipoAjusteConciliacao || tipoCusto == TipoAjusteSaldo;

    // Lançamento que é ajuste pela categoria/descrição, mesmo que algum caminho antigo tenha
    // sobrescrito o TipoCusto (ex.: recategorização, re-salvar o dia no Caixa).
    public static bool PareceAjusteConciliacao(string? tipoCusto, string? categoria, string? descricao) =>
        EhAjusteConciliacao(tipoCusto)
        || string.Equals(categoria?.Trim(), CategoriaAjusteSaldo, StringComparison.OrdinalIgnoreCase)
        || string.Equals(descricao?.Trim(), CategoriaAjusteSaldo, StringComparison.OrdinalIgnoreCase)
        || (descricao?.Trim().StartsWith("Ajuste de conciliação em", StringComparison.OrdinalIgnoreCase) ?? false);

    public static bool EhOperacional(string? tipoCusto) =>
        tipoCusto != TipoTransferencia && tipoCusto != TipoRendimento
        && tipoCusto != TipoInvestimento && tipoCusto != TipoFinanciamento
        && tipoCusto != TipoPagamentoFatura && !EhAjusteConciliacao(tipoCusto);
}
