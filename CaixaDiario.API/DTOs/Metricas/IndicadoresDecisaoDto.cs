namespace CaixaDiario.API.DTOs.Metricas;

public class IndicadoresDecisaoDto
{
    public DreDto Dre { get; set; } = null!;
    public decimal CustoFixo { get; set; }
    public decimal CustoVariavel { get; set; }
    public decimal CustoNaoClassificado { get; set; }
    public List<CategoriaIndicadorDto> RankingCategorias { get; set; } = new();
    public List<EvolucaoMensalDto> Evolucao { get; set; } = new();
    public int MesesComAtividade { get; set; }
    public decimal? VariacaoReceitaMesAnterior { get; set; }
    public decimal? VariacaoReceitaAnoAnterior { get; set; }
}

public class CategoriaIndicadorDto
{
    public string Nome { get; set; } = string.Empty;
    public string Grupo { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public decimal? PercentualReceita { get; set; }
    public decimal? MediaMesesAnteriores { get; set; }
    public decimal? VariacaoPercentual { get; set; }
}
