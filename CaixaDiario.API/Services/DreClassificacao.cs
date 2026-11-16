using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

/// <summary>
/// Um lançamento já colocado na sua linha-folha do DRE (Bloco → Grupo → Categoria). Valor com o
/// sinal do demonstrativo: entrada soma, saída subtrai.
/// </summary>
public sealed record LancamentoDre(
    string Bloco, string Grupo, string Categoria, decimal Valor,
    Guid LancamentoId, Guid? ContaBancariaId, DateOnly Data, string Tipo, string Descricao,
    string? CategoriaOriginal, string? TipoCusto);

/// <summary>Metadados do Plano de Contas usados pra posicionar cada lançamento no DRE.</summary>
public sealed class PlanoDre
{
    public static readonly Dictionary<string, string> MapaGrupoPadrao = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Insumos/Mercadoria"] = "Custos Diretos",
        ["Embalagens"]         = "Custos Diretos",
        ["Comissões"]          = "Custos Diretos",
        ["Salários/Folha"]     = "Pessoas",
        ["Encargos"]           = "Pessoas",
        ["Benefícios"]         = "Pessoas",
        ["Pró-labore"]         = "Pessoas",
        ["Aluguel"]                    = "Despesas Administrativas",
        ["Energia/Água/Internet"]      = "Despesas Administrativas",
        ["Seguros"]                    = "Despesas Administrativas",
        ["Manutenção"]                 = "Despesas Administrativas",
        ["Material de Escritório"]     = "Despesas Administrativas",
        ["Publicidade"]     = "Marketing",
        ["Mídia paga"]      = "Marketing",
        ["Material gráfico"]= "Marketing",
        ["Marketing"]       = "Marketing",
        ["Simples/DAS"]      = "Impostos",
        ["ISS"]              = "Impostos",
        ["Outros tributos"]  = "Impostos",
        ["Tarifas bancárias"]= "Financeiras",
        ["Juros"]            = "Financeiras",
        ["IOF"]              = "Financeiras",
        ["Equipamentos"] = "Investimentos",
        ["Reformas"]     = "Investimentos",
        ["Software"]     = "Investimentos",
    };

    private static readonly string[] _ordemGruposPadrao =
    [
        "Custos Diretos", "Pessoas", "Despesas Administrativas",
        "Marketing", "Impostos", "Financeiras", "Investimentos", "Outros"
    ];

    public Dictionary<string, string> MapaGrupo { get; }
    public string[] OrdemGrupos { get; }
    public Dictionary<string, int>? MapaOrdem { get; }
    public Dictionary<string, string>? MapaTipo { get; }
    // Só populado quando o grupo tem Bloco definido (planos migrados pra hierarquia nova).
    public Dictionary<string, string> MapaBloco { get; }
    // Sem plano (chamadas antigas/testes) não dá pra dizer que uma categoria "não existe".
    public bool TemPlano => MapaTipo != null;

    public PlanoDre(IReadOnlyList<Categoria>? categorias)
    {
        if (categorias is { Count: > 0 })
        {
            MapaGrupo = categorias
                .Where(c => !string.IsNullOrWhiteSpace(c.Grupo?.Nome))
                .ToDictionary(c => c.Nome, c => c.Grupo!.Nome, StringComparer.OrdinalIgnoreCase);
            MapaOrdem = categorias.ToDictionary(c => c.Nome, c => c.Ordem, StringComparer.OrdinalIgnoreCase);
            MapaTipo = categorias.ToDictionary(c => c.Nome, c => c.Tipo, StringComparer.OrdinalIgnoreCase);
            MapaBloco = categorias
                .Where(c => !string.IsNullOrWhiteSpace(c.Grupo?.Bloco))
                .ToDictionary(c => c.Nome, c => c.Grupo!.Bloco, StringComparer.OrdinalIgnoreCase);
            OrdemGrupos = categorias
                .Where(c => !string.IsNullOrWhiteSpace(c.Grupo?.Nome))
                .GroupBy(c => c.Grupo!.Nome, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Min(c => c.Ordem))
                .Select(g => g.Key)
                .Append("Outros")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        else
        {
            MapaGrupo = MapaGrupoPadrao;
            OrdemGrupos = _ordemGruposPadrao;
            MapaBloco = new(StringComparer.OrdinalIgnoreCase);
        }
    }

    public bool CategoriaForaDoPlano(string? categoria) =>
        TemPlano && !string.IsNullOrWhiteSpace(categoria) && !MapaTipo!.ContainsKey(categoria.Trim());

    // A análise vertical e os blocos classificam cada saída pela MESMA regra (Money Vision "três
    // resultados na mesma resposta").
    public string BlocoDaSaida(ItemFinanceiroSaida saida)
    {
        var bloco = MapaBloco.TryGetValue(saida.Categoria ?? "", out var b) ? b : null;
        if (bloco is Blocos.DeducoesDaReceita or Blocos.CustosOperacionais or Blocos.DespesasOperacionais or Blocos.RetiradasSocios)
            return bloco;
        // Sem Bloco (plano de contas antigo): critério histórico por Tipo + grupo "Impostos".
        var grupo = MapaGrupo.TryGetValue(saida.Categoria ?? "", out var g) ? g : null;
        var tipo = MapaTipo != null
            ? (MapaTipo.TryGetValue(saida.Categoria ?? "", out var t) ? t : null)
            : saida.TipoCusto;
        return string.Equals(grupo, "Impostos", StringComparison.OrdinalIgnoreCase) ? Blocos.DeducoesDaReceita
            : tipo == "CustoVariavel" ? Blocos.CustosOperacionais
            : tipo == "RetiradaSocios" ? Blocos.RetiradasSocios
            : Blocos.DespesasOperacionais;
    }
}

/// <summary>
/// Money Vision ("Outras receitas R$ 8.659,99 sem nenhum lançamento"): regra ÚNICA que coloca
/// cada lançamento numa linha do DRE. O total de cada linha e o detalhe (drill-down) saem desta
/// mesma lista — o detalhe nunca refaz o filtro por nome de categoria.
/// </summary>
public static class DreClassificacao
{
    // Fora do resultado: só aparece no "(±) Ajustes de conciliação", depois do Resultado Líquido.
    public const string BlocoAjustes = "AJUSTES DE CONCILIAÇÃO";
    public const string ReceitasAClassificar = "Receitas a classificar";
    public const string DespesasAClassificar = "Despesas a classificar";
    public const string SemCategoria = "Sem categoria";
    // Rendimento de investimento entra na receita (decisão do produto), mas num grupo próprio —
    // nunca mais misturado com a categoria "Outras receitas" do plano.
    public const string GrupoRendimentos = "Rendimentos de investimento";
    public const string SemGrupo = "Não Classificado";

    public static bool EhGrupoAClassificar(string grupo) => grupo is ReceitasAClassificar or DespesasAClassificar;

    public static List<LancamentoDre> Classificar(IEnumerable<RegistroDiario> registros, PlanoDre plano)
    {
        var resultado = new List<LancamentoDre>();
        foreach (var r in registros)
        {
            foreach (var e in r.Entradas)
            {
                var linha = LinhaDaEntrada(e, plano);
                if (linha is { } l)
                    resultado.Add(new LancamentoDre(l.Bloco, l.Grupo, l.Categoria, e.Valor,
                        e.Id, r.ContaBancariaId, r.Data, "Entrada", e.Descricao, e.Categoria, e.TipoCusto));
            }
            foreach (var s in r.Saidas)
            {
                var linha = LinhaDaSaida(s, plano);
                if (linha is { } l)
                    resultado.Add(new LancamentoDre(l.Bloco, l.Grupo, l.Categoria, -s.Valor,
                        s.Id, r.ContaBancariaId, r.Data, "Saida", s.Descricao, s.Categoria, s.TipoCusto));
            }
        }
        return resultado;
    }

    private static (string Bloco, string Grupo, string Categoria)? LinhaDaEntrada(ItemFinanceiro e, PlanoDre plano)
    {
        if (LancamentoFiltro.PareceAjusteConciliacao(e.TipoCusto, e.Categoria, e.Descricao))
            return (BlocoAjustes, LancamentoFiltro.CategoriaAjusteSaldo, LancamentoFiltro.CategoriaAjusteSaldo);
        if (e.TipoCusto == LancamentoFiltro.TipoRendimento)
            return (Blocos.ReceitasOperacionais, GrupoRendimentos, NomeOu(e.Categoria, "Rendimento"));
        if (LinhaDeAtividade(e.TipoCusto, e.Categoria, plano) is { } atividade)
            return atividade;
        if (!LancamentoFiltro.EhOperacional(e.TipoCusto))
            return null;
        if (string.IsNullOrWhiteSpace(e.Categoria))
            return (Blocos.ReceitasOperacionais, ReceitasAClassificar, SemCategoria);
        if (plano.CategoriaForaDoPlano(e.Categoria))
            return (Blocos.ReceitasOperacionais, ReceitasAClassificar, e.Categoria.Trim());
        return (Blocos.ReceitasOperacionais, GrupoDe(e.Categoria, plano), e.Categoria);
    }

    private static (string Bloco, string Grupo, string Categoria)? LinhaDaSaida(ItemFinanceiroSaida s, PlanoDre plano)
    {
        if (LancamentoFiltro.PareceAjusteConciliacao(s.TipoCusto, s.Categoria, s.Descricao))
            return (BlocoAjustes, LancamentoFiltro.CategoriaAjusteSaldo, LancamentoFiltro.CategoriaAjusteSaldo);
        if (s.TipoCusto == LancamentoFiltro.TipoRendimento)
            return (Blocos.ReceitasOperacionais, GrupoRendimentos, NomeOu(s.Categoria, "Rendimento"));
        if (LinhaDeAtividade(s.TipoCusto, s.Categoria, plano) is { } atividade)
            return atividade;
        if (!LancamentoFiltro.EhOperacional(s.TipoCusto))
            return null;
        var bloco = plano.BlocoDaSaida(s);
        if (string.IsNullOrWhiteSpace(s.Categoria))
            return (bloco, DespesasAClassificar, SemCategoria);
        if (plano.CategoriaForaDoPlano(s.Categoria))
            return (bloco, DespesasAClassificar, s.Categoria.Trim());
        return (bloco, GrupoDe(s.Categoria, plano), s.Categoria);
    }

    // Investimento/Financiamento: fato modificativo pós-operacional; o sinal vem de ser entrada ou saída.
    private static (string Bloco, string Grupo, string Categoria)? LinhaDeAtividade(string? tipoCusto, string? categoria, PlanoDre plano)
    {
        var bloco = tipoCusto switch
        {
            LancamentoFiltro.TipoInvestimento => Blocos.AtividadesDeInvestimento,
            LancamentoFiltro.TipoFinanciamento => Blocos.AtividadesDeFinanciamento,
            _ => null,
        };
        if (bloco == null) return null;
        var nome = NomeOu(categoria, SemGrupo);
        return (bloco, GrupoDe(nome, plano), nome);
    }

    private static string GrupoDe(string categoria, PlanoDre plano) =>
        plano.MapaGrupo.TryGetValue(categoria, out var g) ? g : SemGrupo;

    private static string NomeOu(string? nome, string padrao) => string.IsNullOrWhiteSpace(nome) ? padrao : nome;
}
