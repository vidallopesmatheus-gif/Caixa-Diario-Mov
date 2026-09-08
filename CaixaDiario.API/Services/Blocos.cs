namespace CaixaDiario.API.Services;

/// <summary>
/// Os 6 blocos são fixos na estrutura do demonstrativo — não são cadastráveis pelo usuário
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

    public static readonly string[] Ordem =
    [
        ReceitasOperacionais, DeducoesDaReceita, CustosOperacionais,
        DespesasOperacionais, AtividadesDeInvestimento, AtividadesDeFinanciamento,
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
        _ => throw new ArgumentOutOfRangeException(nameof(bloco), bloco, "Bloco inválido."),
    };
}
