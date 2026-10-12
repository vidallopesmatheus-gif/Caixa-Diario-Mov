namespace CaixaDiario.API.Services;

/// <summary>
/// Os 7 blocos são fixos na estrutura do demonstrativo — não são cadastráveis pelo usuário
/// (só os Grupos dentro de cada um são). A ordem da lista é a ordem de exibição no DRE.
/// </summary>
public static class Blocos
{
    public const string ReceitasOperacionais = "RECEITAS OPERACIONAIS";
    public const string DeducoesDaReceita = "DEDUÇÕES DA RECEITA";
    public const string CustosOperacionais = "CUSTOS OPERACIONAIS";
    public const string DespesasOperacionais = "DESPESAS OPERACIONAIS";
    public const string AtividadesDeInvestimento = "ATIVIDADES DE INVESTIMENTO";
    public const string AtividadesDeFinanciamento = "ATIVIDADES DE FINANCIAMENTO";
    // Correção de integridade (Parte 1, item 6): Pró-labore/retiradas dos sócios nunca foi
    // despesa operacional nem atividade de investimento/financiamento — é a ÚLTIMA linha antes
    // do Resultado Líquido (depois de Investimento e Financiamento), por decisão explícita do
    // usuário (não é "despesa fixa" nem "atividade", é distribuição do resultado aos sócios).
    public const string RetiradasSocios = "RETIRADAS DOS SÓCIOS";

    public static readonly string[] Ordem =
    [
        ReceitasOperacionais, DeducoesDaReceita, CustosOperacionais, DespesasOperacionais,
        AtividadesDeInvestimento, AtividadesDeFinanciamento, RetiradasSocios,
    ];

    public static readonly HashSet<string> Validos = new(Ordem);

    /// <summary>
    /// Tipo de categoria (Categoria.Tipo) atribuído automaticamente conforme o Bloco do grupo
    /// escolhido — o usuário não escolhe mais o Tipo diretamente na criação da categoria.
    /// </summary>
    public static string TipoPadrao(string bloco) => bloco switch
    {
        ReceitasOperacionais => "Receita",
        DeducoesDaReceita => "CustoFixo",
        CustosOperacionais => "CustoVariavel",
        DespesasOperacionais => "CustoFixo",
        AtividadesDeInvestimento => "Investimento",
        AtividadesDeFinanciamento => "Financiamento",
        RetiradasSocios => "RetiradaSocios",
        _ => throw new ArgumentOutOfRangeException(nameof(bloco), bloco, "Bloco inválido."),
    };
}
