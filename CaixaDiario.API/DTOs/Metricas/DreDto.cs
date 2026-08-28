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
    public decimal ResultadoLiquido { get; set; }
    public decimal? ResultadoLiquidoPercentual { get; set; }
}
