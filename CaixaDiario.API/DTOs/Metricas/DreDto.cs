namespace CaixaDiario.API.DTOs.Metricas;

public class DreCategoriaDto
{
    public string Nome { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public decimal? Percentual { get; set; }
}

public class DreLinhaDto
{
    public string Grupo { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public List<DreCategoriaDto> Categorias { get; set; } = new();
}

/// <summary>Uma linha da análise vertical (Deduções, Custos Variáveis, Despesas Fixas, ...).</summary>
public class DreLinhaVerticalDto
{
    public decimal Total { get; set; }
    public decimal? Percentual { get; set; }
    public List<DreCategoriaDto> Categorias { get; set; } = new();
}

/// <summary>Nível "Grupo" da árvore Bloco → Grupo → Categoria do demonstrativo de 3 níveis.</summary>
public class DreGrupoDto
{
    public string Nome { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public decimal? Percentual { get; set; }
    public List<DreCategoriaDto> Categorias { get; set; } = new();
}

/// <summary>Nível "Bloco" (fixo — RECEITAS OPERACIONAIS, DEDUÇÕES DA RECEITA, ...) do demonstrativo.</summary>
public class DreBlocoDto
{
    public string Bloco { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public decimal? Percentual { get; set; }
    public List<DreGrupoDto> Grupos { get; set; } = new();
}

public class DreDto
{
    public decimal ReceitaBruta { get; set; }
    public List<DreLinhaDto> GruposDespesa { get; set; } = new();
    public decimal TotalDespesas { get; set; }
    public decimal Resultado { get; set; }
    public decimal? Margem { get; set; }

    // Análise vertical (base: Receita Bruta = 100%)
    public decimal? ReceitaBrutaPercentual { get; set; }
    public DreLinhaVerticalDto Deducoes { get; set; } = new();
    public decimal ReceitaLiquida { get; set; }
    public decimal? ReceitaLiquidaPercentual { get; set; }
    public DreLinhaVerticalDto CustosVariaveis { get; set; } = new();
    public decimal MargemContribuicao { get; set; }
    public decimal? MargemContribuicaoPercentual { get; set; }
    public DreLinhaVerticalDto DespesasFixas { get; set; } = new();
    public decimal ResultadoOperacional { get; set; }
    public decimal? ResultadoOperacionalPercentual { get; set; }
    // Receita financeira (rendimento de investimento): nunca soma na receita operacional nem na
    // margem operacional — entra só aqui, depois do Resultado Operacional, e flui pro Resultado Líquido.
    public DreLinhaVerticalDto ReceitaFinanceira { get; set; } = new();
    public DreLinhaVerticalDto DespesasNaoOperacionais { get; set; } = new();
    public DreLinhaVerticalDto NaoClassificado { get; set; } = new();
    // Sinal líquido (entrada − saída) por natureza — podem ser positivos (captação, rendimento) ou
    // negativos (amortização, aquisição de imobilizado); nunca assumidos como saída.
    public DreLinhaVerticalDto AtividadesInvestimento { get; set; } = new();
    public DreLinhaVerticalDto AtividadesFinanciamento { get; set; } = new();
    public decimal ResultadoLiquido { get; set; }
    public decimal? ResultadoLiquidoPercentual { get; set; }

    /// <summary>Demonstrativo hierárquico de 3 níveis (Bloco → Grupo → Categoria) para a tela de DRE.</summary>
    public List<DreBlocoDto> Blocos { get; set; } = new();

    // ── Painel de KPIs da tela de DRE — preenchidos pelo controller após CalcularDre, não fazem
    // parte do cálculo do demonstrativo em si (ver MetricasController.ObterDre). Nulo quando não
    // solicitado (ex.: chamadas internas do CalcularIndicadores, que não usam esses campos).
    public PontoEquilibrioDetalhadoDto? PontoEquilibrio { get; set; }
    public List<ResultadoLiquidoMensalDto>? EvolucaoResultadoLiquido { get; set; }
}

/// <summary>Um mês da série usada no sparkline de Resultado Líquido do painel de KPIs da DRE.</summary>
public class ResultadoLiquidoMensalDto
{
    public string Mes { get; set; } = string.Empty;
    public decimal ResultadoLiquido { get; set; }
    public decimal? ResultadoLiquidoPercentual { get; set; }
    public bool TemDados { get; set; }
}
