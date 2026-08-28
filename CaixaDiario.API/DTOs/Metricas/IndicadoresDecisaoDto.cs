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

    // ── Indicadores de decisão (aprofundamento da subaba Indicadores) ──
    public PontoEquilibrioDetalhadoDto PontoEquilibrio { get; set; } = new();
    public FolegoCaixaDto FolegoCaixa { get; set; } = new();
    public List<CustoFixoMensalDto> CustoFixoMensal { get; set; } = new();
    public PrazoRecebimentoDto PrazoRecebimento { get; set; } = new();
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

/// <summary>"Quanto preciso vender pra não ter prejuízo?" — usa a Margem de Contribuição do DRE do mês.</summary>
public class PontoEquilibrioDetalhadoDto
{
    public bool Disponivel { get; set; }
    public string? MotivoIndisponivel { get; set; }
    public decimal? ValorMensal { get; set; }
    public decimal? ValorPorDiaUtil { get; set; }
    public int? DiasUteisNoMes { get; set; }
    public decimal ReceitaAtual { get; set; }
    public decimal? Distancia { get; set; }
    public decimal? DistanciaPercentual { get; set; }
}

/// <summary>"Se parar de entrar dinheiro, quanto tempo eu duro?" — saldo em caixa ÷ custo fixo médio.</summary>
public class FolegoCaixaDto
{
    public bool Disponivel { get; set; }
    public string? MotivoIndisponivel { get; set; }
    public decimal SaldoDisponivel { get; set; }
    public decimal? CustoFixoMedioMensal { get; set; }
    public decimal? Meses { get; set; }
    /// <summary>"critico" (&lt;1), "atencao" (1–3) ou "confortavel" (&gt;3).</summary>
    public string Faixa { get; set; } = "indisponivel";
}

/// <summary>Um mês da série usada pra tendência de Custo Fixo ÷ Receita.</summary>
public class CustoFixoMensalDto
{
    public string Mes { get; set; } = string.Empty;
    public decimal Receita { get; set; }
    public decimal CustoFixo { get; set; }
    public decimal? Percentual { get; set; }
}

/// <summary>"Meu problema é faturamento ou é recebimento?" — atraso médio entre vencimento e baixa.</summary>
public class PrazoRecebimentoDto
{
    public bool Disponivel { get; set; }
    public string? MotivoIndisponivel { get; set; }
    public decimal? MediaDias { get; set; }
    public int QuantidadeAmostras { get; set; }
}
