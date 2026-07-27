using CaixaDiario.API.DTOs.Metricas;
using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

public class MetricasService : IMetricasService
{
    public MetricasPeriodoDto CalcularPeriodo(List<RegistroDiario> todosRegistros, List<RegistroDiario> registrosDoPeriodo, decimal multiplo = 3m)
    {
        var entradas = registrosDoPeriodo.SelectMany(r => r.Entradas).ToList();
        var saidas = registrosDoPeriodo.SelectMany(r => r.Saidas).ToList();

        var receita = entradas.Where(e => e.TipoCusto == "Receita").Sum(e => e.Valor);
        var custosFixos = saidas.Where(s => s.TipoCusto == "CustoFixo" && s.Categoria != "Manutenção").Sum(s => s.Valor);
        var custosVariaveis = saidas.Where(s => s.TipoCusto == "CustoVariavel").Sum(s => s.Valor);

        var temCategoria = entradas.Any(e => e.Categoria != null) || saidas.Any(s => s.Categoria != null);

        var dto = new MetricasPeriodoDto();

        if (temCategoria && receita > 0)
        {
            var ebitdaValor = receita - custosFixos - custosVariaveis;
            var ebitdaPerc = ebitdaValor / receita;
            dto.Ebitda = new EbitdaDto
            {
                Valor = ebitdaValor,
                Percentual = ebitdaPerc,
                Semaforo = ebitdaPerc >= 0.15m ? "verde" : ebitdaPerc >= 0.05m ? "amarelo" : "vermelho",
            };

            var salarios = saidas.Where(s => s.Categoria == "Salários/Folha").Sum(s => s.Valor);
            var insumos = saidas.Where(s => s.Categoria == "Insumos/Mercadoria").Sum(s => s.Valor);
            if (salarios > 0 || insumos > 0)
            {
                var primeCostPerc = (salarios + insumos) / receita;
                dto.PrimeCost = new PrimeCostDto
                {
                    Percentual = primeCostPerc,
                    Semaforo = primeCostPerc < 0.6m ? "verde" : primeCostPerc <= 0.75m ? "amarelo" : "vermelho",
                };
            }

            if (custosFixos > 0 || custosVariaveis > 0)
            {
                var mc = (receita - custosVariaveis) / receita;
                var pe = mc > 0 ? custosFixos / mc : 0;
                dto.PontoDeEquilibrio = new PontoDeEquilibrioDto
                {
                    Valor = pe,
                    Receita = receita,
                    Semaforo = receita >= pe * 1.2m ? "verde" : receita >= pe ? "amarelo" : "vermelho",
                };
            }

            var qtdRecebimentos = entradas.Count(e => e.TipoCusto == "Receita");
            if (qtdRecebimentos > 0)
            {
                dto.TicketMedio = new TicketMedioDto
                {
                    Valor = Math.Round(receita / qtdRecebimentos, 2),
                    QuantidadeRecebimentos = qtdRecebimentos,
                };
            }
        }

        var saldoAtual = todosRegistros.OrderByDescending(r => r.Data).FirstOrDefault()?.SaldoFinal ?? 0;
        var totalReceber = todosRegistros.SelectMany(r => r.ContasReceber).Where(c => !c.Pago).Sum(c => c.Valor);
        var totalPagar = todosRegistros.SelectMany(r => r.ContasPagar).Where(c => !c.Pago).Sum(c => c.Valor);
        dto.SaldoProjetado = saldoAtual + totalReceber - totalPagar;

        // Valuation
        var ultimos3Meses = Enumerable.Range(0, 3)
            .Select(i => DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-i))
            .Select(m => todosRegistros.Where(r => r.Data.Year == m.Year && r.Data.Month == m.Month).ToList())
            .ToList();

        if (ultimos3Meses.Any(m => m.Count > 0))
        {
            var lucrosMensais = ultimos3Meses.Select(m =>
                m.SelectMany(r => r.Entradas).Sum(e => e.Valor) -
                m.SelectMany(r => r.Saidas).Sum(s => s.Valor)).ToList();
            var lucroMedioMensal = lucrosMensais.Average();
            var valuationValor = lucroMedioMensal * 12 * multiplo;
            string valuationSemaforo = "cinza";
            if (ultimos3Meses[0].Count > 0 && ultimos3Meses[1].Count > 0)
            {
                var lucroAtual = lucrosMensais[0];
                var lucroAnterior = lucrosMensais[1];
                valuationSemaforo = lucroAnterior == 0 ? "cinza"
                    : (lucroAtual - lucroAnterior) / Math.Abs(lucroAnterior) > 0.05m ? "verde"
                    : (lucroAtual - lucroAnterior) / Math.Abs(lucroAnterior) < -0.05m ? "vermelho"
                    : "amarelo";
            }
            dto.Valuation = new ValuationDto { Valor = valuationValor, Semaforo = valuationSemaforo };
        }

        // Runway
        var burnMedioMensal = ultimos3Meses
            .Where(m => m.Count > 0)
            .Select(m => m.SelectMany(r => r.Saidas).Sum(s => s.Valor))
            .DefaultIfEmpty(0)
            .Average();
        dto.BurnRate = burnMedioMensal > 0 ? Math.Round(burnMedioMensal, 2) : (decimal?)null;
        var runwayMeses = burnMedioMensal > 0 ? Math.Round(saldoAtual / burnMedioMensal, 1) : 0;

        dto.Runway = new RunwayDto
        {
            Meses = runwayMeses,
            Semaforo = burnMedioMensal == 0 ? "cinza"
                : runwayMeses > 6 ? "verde"
                : runwayMeses >= 3 ? "amarelo"
                : "vermelho",
        };

        // Liquidez
        var hoje30 = DateOnly.FromDateTime(DateTime.UtcNow);
        var em30dias = hoje30.AddDays(30);
        var contasPagarProximas = todosRegistros
            .SelectMany(r => r.ContasPagar)
            .Where(c => !c.Pago && c.DataVencimento.HasValue &&
                        c.DataVencimento.Value >= hoje30 && c.DataVencimento.Value <= em30dias)
            .Sum(c => c.Valor);

        var contasReceberProximas = todosRegistros
            .SelectMany(r => r.ContasReceber)
            .Where(c => !c.Pago && c.DataVencimento.HasValue &&
                        c.DataVencimento.Value >= hoje30 && c.DataVencimento.Value <= em30dias)
            .Sum(c => c.Valor);
        var numeradorLiquidez = saldoAtual + contasReceberProximas;

        if (contasPagarProximas == 0)
        {
            dto.Liquidez = new LiquidezDto { AltaLiquidez = true, Semaforo = "verde" };
        }
        else
        {
            var indice = Math.Round(numeradorLiquidez / contasPagarProximas, 2);
            dto.Liquidez = new LiquidezDto
            {
                Indice = indice,
                AltaLiquidez = false,
                Semaforo = indice >= 1.5m ? "verde" : indice >= 1.0m ? "amarelo" : "vermelho",
            };
        }

        return dto;
    }

    public List<EvolucaoMensalDto> CalcularEvolucao(List<RegistroDiario> registros, int meses)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var resultado = new List<EvolucaoMensalDto>();

        for (int i = meses - 1; i >= 0; i--)
        {
            var ref_ = hoje.AddMonths(-i);
            var prefixo = $"{ref_.Year}-{ref_.Month:D2}";
            var doMes = registros.Where(r => r.Data.ToString("yyyy-MM").StartsWith(prefixo)).ToList();

            var receita = doMes.SelectMany(r => r.Entradas).Sum(e => e.Valor);
            var custos = doMes.SelectMany(r => r.Saidas).Sum(s => s.Valor);
            var saldo = doMes.OrderByDescending(r => r.Data).FirstOrDefault()?.SaldoFinal ?? 0;

            resultado.Add(new EvolucaoMensalDto
            {
                Mes = prefixo,
                Receita = receita,
                Custos = custos,
                Lucro = receita - custos,
                Saldo = saldo,
            });
        }

        return resultado;
    }

    private static readonly Dictionary<string, string> _mapaGrupo = new(StringComparer.OrdinalIgnoreCase)
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

    private static readonly string[] _ordemGrupos =
    [
        "Custos Diretos", "Pessoas", "Despesas Administrativas",
        "Marketing", "Impostos", "Financeiras", "Investimentos", "Outros"
    ];

    public DreDto CalcularDre(List<RegistroDiario> registros)
    {
        var receitaBruta = registros.SelectMany(r => r.Entradas).Sum(e => e.Valor);

        // Agrupa saídas por grupo → categoria
        var porGrupo = new Dictionary<string, Dictionary<string, decimal>>();
        foreach (var saida in registros.SelectMany(r => r.Saidas))
        {
            var grupo = _mapaGrupo.TryGetValue(saida.Categoria ?? "", out var g) ? g : "Outros";
            if (!porGrupo.ContainsKey(grupo))
                porGrupo[grupo] = new Dictionary<string, decimal>();
            var cat = string.IsNullOrWhiteSpace(saida.Categoria) ? "Não Classificado" : saida.Categoria;
            porGrupo[grupo][cat] = (porGrupo[grupo].TryGetValue(cat, out var v) ? v : 0m) + saida.Valor;
        }

        var linhas = _ordemGrupos
            .Where(porGrupo.ContainsKey)
            .Select(grupo =>
            {
                var cats = porGrupo[grupo]
                    .OrderByDescending(kv => kv.Value)
                    .Select(kv => new DreCategoriaDto { Nome = kv.Key, Total = kv.Value })
                    .ToList();
                return new DreLinhaDto
                {
                    Grupo = grupo,
                    Total = cats.Sum(c => c.Total),
                    Categorias = cats,
                };
            })
            .ToList();

        var totalDespesas = linhas.Sum(l => l.Total);
        var resultado = receitaBruta - totalDespesas;

        return new DreDto
        {
            ReceitaBruta  = receitaBruta,
            GruposDespesa = linhas,
            TotalDespesas = totalDespesas,
            Resultado     = resultado,
            Margem        = receitaBruta > 0 ? Math.Round(resultado / receitaBruta * 100, 1) : null,
        };
    }

    public IndicadoresDecisaoDto CalcularIndicadores(List<RegistroDiario> registros, int mesesEvolucao = 13)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var doMesAtual = registros.Where(r => r.Data.Year == hoje.Year && r.Data.Month == hoje.Month).ToList();

        var dre = CalcularDre(doMesAtual);
        var evolucao = CalcularEvolucao(registros, mesesEvolucao);

        // Fixo x Variável x Não Classificado — sempre reconcilia com dre.TotalDespesas
        var saidasMes = doMesAtual.SelectMany(r => r.Saidas).ToList();
        var custoFixo = saidasMes.Where(s => s.TipoCusto == "CustoFixo").Sum(s => s.Valor);
        var custoVariavel = saidasMes.Where(s => s.TipoCusto == "CustoVariavel").Sum(s => s.Valor);
        var custoNaoClassificado = dre.TotalDespesas - custoFixo - custoVariavel;

        // Ranking de categorias: achata dre.GruposDespesa (já calculado) e agrega % da receita + comparação
        // com a média dos 3 meses anteriores, extrapolando o mês atual (parcial) para o mês cheio.
        var mesesAnteriores = Enumerable.Range(1, 3)
            .Select(i => hoje.AddMonths(-i))
            .Select(m => SomarSaidasPorCategoria(registros.Where(r => r.Data.Year == m.Year && r.Data.Month == m.Month)))
            .ToList();

        var diasNoMes = DateTime.DaysInMonth(hoje.Year, hoje.Month);
        var ranking = dre.GruposDespesa
            .SelectMany(g => g.Categorias.Select(c => (Grupo: g.Grupo, c.Nome, c.Total)))
            .Select(c =>
            {
                var mediasEncontradas = mesesAnteriores.Select(m => m.TryGetValue(c.Nome, out var v) ? v : 0m).ToList();
                var media = mediasEncontradas.Any(v => v > 0) ? mediasEncontradas.Average() : (decimal?)null;
                var extrapolado = hoje.Day > 0 ? c.Total / hoje.Day * diasNoMes : c.Total;
                return new CategoriaIndicadorDto
                {
                    Nome = c.Nome,
                    Grupo = c.Grupo,
                    Total = c.Total,
                    PercentualReceita = dre.ReceitaBruta > 0 ? Math.Round(c.Total / dre.ReceitaBruta * 100, 1) : null,
                    MediaMesesAnteriores = media,
                    VariacaoPercentual = media is > 0 ? Math.Round((extrapolado - media.Value) / media.Value * 100, 1) : null,
                };
            })
            .OrderByDescending(c => c.Total)
            .ToList();

        var mesesComAtividade = evolucao.Count(e => e.Receita > 0 || e.Custos > 0);

        decimal? variacaoMoM = null;
        decimal? variacaoYoY = null;
        if (evolucao.Count >= 2)
        {
            var atual = evolucao[^1];
            var anterior = evolucao[^2];
            if (anterior.Receita > 0)
                variacaoMoM = Math.Round((atual.Receita - anterior.Receita) / anterior.Receita * 100, 1);
        }
        if (evolucao.Count >= 13)
        {
            var atual = evolucao[^1];
            var mesmoMesAnoAnterior = evolucao[^13];
            if (mesmoMesAnoAnterior.Receita > 0)
                variacaoYoY = Math.Round((atual.Receita - mesmoMesAnoAnterior.Receita) / mesmoMesAnoAnterior.Receita * 100, 1);
        }

        return new IndicadoresDecisaoDto
        {
            Dre = dre,
            CustoFixo = custoFixo,
            CustoVariavel = custoVariavel,
            CustoNaoClassificado = custoNaoClassificado,
            RankingCategorias = ranking,
            Evolucao = evolucao,
            MesesComAtividade = mesesComAtividade,
            VariacaoReceitaMesAnterior = variacaoMoM,
            VariacaoReceitaAnoAnterior = variacaoYoY,
        };
    }

    private static Dictionary<string, decimal> SomarSaidasPorCategoria(IEnumerable<RegistroDiario> registros)
    {
        var dict = new Dictionary<string, decimal>();
        foreach (var s in registros.SelectMany(r => r.Saidas))
        {
            var cat = string.IsNullOrWhiteSpace(s.Categoria) ? "Não Classificado" : s.Categoria;
            dict[cat] = (dict.TryGetValue(cat, out var v) ? v : 0m) + s.Valor;
        }
        return dict;
    }

    public FluxoProjetadoDto CalcularFluxoProjetado(List<RegistroDiario> registros, List<ContaRecorrente> recorrentes, int dias)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var saldoAtual = registros.OrderByDescending(r => r.Data).FirstOrDefault()?.SaldoFinal ?? 0;

        var fluxoDias = new List<FluxoDiaDto>();
        var saldoCorrendo = saldoAtual;

        for (int d = 1; d <= dias; d++)
        {
            var dia = hoje.AddDays(d);

            var entradas = registros.SelectMany(r => r.ContasReceber)
                .Where(c => !c.Pago && c.DataVencimento == dia).Sum(c => c.Valor);

            var saidas = registros.SelectMany(r => r.ContasPagar)
                .Where(c => !c.Pago && c.DataVencimento == dia).Sum(c => c.Valor);

            var entradasRec = recorrentes.Where(r => r.Tipo == "Receber" && r.Ativo &&
                RecorrenciaService.OcorreEm(r, dia)).Sum(r => r.Valor);

            var saidasRec = recorrentes.Where(r => r.Tipo == "Pagar" && r.Ativo &&
                RecorrenciaService.OcorreEm(r, dia)).Sum(r => r.Valor);

            saldoCorrendo += entradas + entradasRec - saidas - saidasRec;

            fluxoDias.Add(new FluxoDiaDto { Data = dia, SaldoProjetado = saldoCorrendo });
        }

        return new FluxoProjetadoDto { SaldoAtual = saldoAtual, Dias = fluxoDias };
    }
}
