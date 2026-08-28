using CaixaDiario.API.DTOs.Metricas;
using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

public class MetricasService : IMetricasService
{
    public MetricasPeriodoDto CalcularPeriodo(List<RegistroDiario> todosRegistros, List<RegistroDiario> registrosDoPeriodo, decimal multiplo = 3m)
    {
        var entradas = registrosDoPeriodo.SelectMany(r => r.Entradas).Where(e => LancamentoFiltro.EhOperacional(e.TipoCusto)).ToList();
        var saidas = registrosDoPeriodo.SelectMany(r => r.Saidas).Where(s => LancamentoFiltro.EhOperacional(s.TipoCusto)).ToList();

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
                m.SelectMany(r => r.Entradas).Where(e => LancamentoFiltro.EhOperacional(e.TipoCusto)).Sum(e => e.Valor) -
                m.SelectMany(r => r.Saidas).Where(s => LancamentoFiltro.EhOperacional(s.TipoCusto)).Sum(s => s.Valor)).ToList();
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
            .Select(m => m.SelectMany(r => r.Saidas).Where(s => LancamentoFiltro.EhOperacional(s.TipoCusto)).Sum(s => s.Valor))
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

            var receita = doMes.SelectMany(r => r.Entradas).Where(e => LancamentoFiltro.EhOperacional(e.TipoCusto)).Sum(e => e.Valor);
            var custos = doMes.SelectMany(r => r.Saidas).Where(s => LancamentoFiltro.EhOperacional(s.TipoCusto)).Sum(s => s.Valor);
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

    public DreDto CalcularDre(List<RegistroDiario> registros, IReadOnlyList<Categoria>? categorias = null)
    {
        // Transferências entre contas e rendimento de investimento não são receita/despesa —
        // ficam de fora do DRE (continuam visíveis no extrato/Caixa, só não entram no resultado).
        var saidasOperacionais = registros.SelectMany(r => r.Saidas).Where(s => LancamentoFiltro.EhOperacional(s.TipoCusto)).ToList();
        var receitaBruta = registros.SelectMany(r => r.Entradas).Where(e => LancamentoFiltro.EhOperacional(e.TipoCusto)).Sum(e => e.Valor);

        // Receita financeira (rendimento de investimento): fato modificativo, mas não é receita
        // operacional — nunca soma em receitaBruta. Correções negativas de rendimento são lançadas
        // como saída com o mesmo TipoCusto e reduzem esta linha.
        var receitaFinanceiraCats = new Dictionary<string, decimal>();
        foreach (var entrada in registros.SelectMany(r => r.Entradas).Where(e => e.TipoCusto == LancamentoFiltro.TipoRendimento))
            receitaFinanceiraCats["Rendimento"] = (receitaFinanceiraCats.TryGetValue("Rendimento", out var vEnt) ? vEnt : 0m) + entrada.Valor;
        foreach (var saidaRend in registros.SelectMany(r => r.Saidas).Where(s => s.TipoCusto == LancamentoFiltro.TipoRendimento))
            receitaFinanceiraCats["Rendimento"] = (receitaFinanceiraCats.TryGetValue("Rendimento", out var vSai) ? vSai : 0m) - saidaRend.Valor;

        // Metadados de grupo/ordem: vêm do Plano de Contas (Configurações) quando disponível;
        // caem para os mapas fixos históricos quando não (ex.: chamadas antigas/testes sem categorias).
        Dictionary<string, string> mapaGrupo;
        string[] ordemGrupos;
        Dictionary<string, int>? mapaOrdem = null;

        Dictionary<string, string>? mapaTipo = null;

        if (categorias is { Count: > 0 })
        {
            mapaGrupo = categorias
                .Where(c => !string.IsNullOrWhiteSpace(c.Grupo))
                .ToDictionary(c => c.Nome, c => c.Grupo!, StringComparer.OrdinalIgnoreCase);
            mapaOrdem = categorias.ToDictionary(c => c.Nome, c => c.Ordem, StringComparer.OrdinalIgnoreCase);
            mapaTipo = categorias.ToDictionary(c => c.Nome, c => c.Tipo, StringComparer.OrdinalIgnoreCase);
            ordemGrupos = categorias
                .Where(c => !string.IsNullOrWhiteSpace(c.Grupo))
                .GroupBy(c => c.Grupo!, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Min(c => c.Ordem))
                .Select(g => g.Key)
                .Append("Outros")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        else
        {
            mapaGrupo = _mapaGrupo;
            ordemGrupos = _ordemGrupos;
        }

        // Agrupa saídas por grupo → categoria
        var porGrupo = new Dictionary<string, Dictionary<string, decimal>>();
        foreach (var saida in saidasOperacionais)
        {
            var grupo = mapaGrupo.TryGetValue(saida.Categoria ?? "", out var g) ? g : "Outros";
            if (!porGrupo.ContainsKey(grupo))
                porGrupo[grupo] = new Dictionary<string, decimal>();
            var cat = string.IsNullOrWhiteSpace(saida.Categoria) ? "Não Classificado" : saida.Categoria;
            porGrupo[grupo][cat] = (porGrupo[grupo].TryGetValue(cat, out var v) ? v : 0m) + saida.Valor;
        }

        var linhas = ordemGrupos
            .Where(porGrupo.ContainsKey)
            .Select(grupo =>
            {
                // Ordem manual (Plano de Contas) manda quando disponível; senão, maior valor primeiro (comportamento histórico).
                var cats = porGrupo[grupo]
                    .OrderBy(kv => mapaOrdem != null && mapaOrdem.TryGetValue(kv.Key, out var ordem) ? ordem : int.MaxValue)
                    .ThenByDescending(kv => kv.Value)
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

        // ---- Análise vertical: mesma lista de saídas, agora em cascata por Tipo ----
        // (-) Deduções/Impostos → grupo "Impostos" (reaproveita o Grupo já existente no Plano de Contas)
        // (-) Custos Variáveis → Tipo == CustoVariavel
        // (-) Despesas Fixas → Tipo == CustoFixo (exceto o que já caiu em Deduções)
        // (-) Despesas Não Operacionais → Tipo == DespesaNaoOperacional
        // (-) Não Classificado → categoria ausente/não cadastrada ou sem Tipo reconhecido (regra: nunca omitir)
        var deducoesCats = new Dictionary<string, decimal>();
        var custosVariaveisCats = new Dictionary<string, decimal>();
        var despesasFixasCats = new Dictionary<string, decimal>();
        var despesasNaoOperacionaisCats = new Dictionary<string, decimal>();
        var naoClassificadoCats = new Dictionary<string, decimal>();

        foreach (var saida in saidasOperacionais)
        {
            var nomeCat = string.IsNullOrWhiteSpace(saida.Categoria) ? "Não Classificado" : saida.Categoria;
            var grupo = mapaGrupo.TryGetValue(saida.Categoria ?? "", out var g) ? g : null;
            var tipo = mapaTipo != null
                ? (mapaTipo.TryGetValue(saida.Categoria ?? "", out var t) ? t : null)
                : saida.TipoCusto;

            var bucket = string.Equals(grupo, "Impostos", StringComparison.OrdinalIgnoreCase) ? deducoesCats
                : tipo == "CustoVariavel" ? custosVariaveisCats
                : tipo == "CustoFixo" ? despesasFixasCats
                : tipo == "DespesaNaoOperacional" ? despesasNaoOperacionaisCats
                : naoClassificadoCats;

            bucket[nomeCat] = (bucket.TryGetValue(nomeCat, out var v) ? v : 0m) + saida.Valor;
        }

        decimal? Percentual(decimal valor) => receitaBruta > 0 ? Math.Round(valor / receitaBruta * 100, 1) : null;

        DreLinhaVerticalDto MontarLinhaVertical(Dictionary<string, decimal> cats)
        {
            var total = cats.Values.Sum();
            return new DreLinhaVerticalDto
            {
                Total = total,
                Percentual = Percentual(total),
                Categorias = cats
                    .OrderByDescending(kv => kv.Value)
                    .Select(kv => new DreCategoriaDto { Nome = kv.Key, Total = kv.Value, Percentual = Percentual(kv.Value) })
                    .ToList(),
            };
        }

        var deducoes = MontarLinhaVertical(deducoesCats);
        var receitaLiquida = receitaBruta - deducoes.Total;
        var custosVariaveis = MontarLinhaVertical(custosVariaveisCats);
        var margemContribuicao = receitaLiquida - custosVariaveis.Total;
        var despesasFixas = MontarLinhaVertical(despesasFixasCats);
        var resultadoOperacional = margemContribuicao - despesasFixas.Total;
        var receitaFinanceira = MontarLinhaVertical(receitaFinanceiraCats);
        var despesasNaoOperacionais = MontarLinhaVertical(despesasNaoOperacionaisCats);
        var naoClassificado = MontarLinhaVertical(naoClassificadoCats);
        var resultadoLiquido = resultadoOperacional + receitaFinanceira.Total - despesasNaoOperacionais.Total - naoClassificado.Total;

        return new DreDto
        {
            ReceitaBruta  = receitaBruta,
            GruposDespesa = linhas,
            TotalDespesas = totalDespesas,
            Resultado     = resultado,
            Margem        = receitaBruta > 0 ? Math.Round(resultado / receitaBruta * 100, 1) : null,

            ReceitaBrutaPercentual = Percentual(receitaBruta),
            Deducoes = deducoes,
            ReceitaLiquida = receitaLiquida,
            ReceitaLiquidaPercentual = Percentual(receitaLiquida),
            CustosVariaveis = custosVariaveis,
            MargemContribuicao = margemContribuicao,
            MargemContribuicaoPercentual = Percentual(margemContribuicao),
            DespesasFixas = despesasFixas,
            ResultadoOperacional = resultadoOperacional,
            ResultadoOperacionalPercentual = Percentual(resultadoOperacional),
            ReceitaFinanceira = receitaFinanceira,
            DespesasNaoOperacionais = despesasNaoOperacionais,
            NaoClassificado = naoClassificado,
            ResultadoLiquido = resultadoLiquido,
            ResultadoLiquidoPercentual = Percentual(resultadoLiquido),
        };
    }

    public IndicadoresDecisaoDto CalcularIndicadores(List<RegistroDiario> registros, int mesesEvolucao = 13, IReadOnlyList<Categoria>? categorias = null)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var doMesAtual = registros.Where(r => r.Data.Year == hoje.Year && r.Data.Month == hoje.Month).ToList();

        var dre = CalcularDre(doMesAtual, categorias);
        var evolucao = CalcularEvolucao(registros, mesesEvolucao);

        // Fixo x Variável x Não Classificado — sempre reconcilia com dre.TotalDespesas
        var saidasMes = doMesAtual.SelectMany(r => r.Saidas).Where(s => LancamentoFiltro.EhOperacional(s.TipoCusto)).ToList();
        var custoFixo = saidasMes.Where(s => s.TipoCusto == "CustoFixo").Sum(s => s.Valor);
        var custoVariavel = saidasMes.Where(s => s.TipoCusto == "CustoVariavel").Sum(s => s.Valor);
        var custoNaoClassificado = dre.TotalDespesas - custoFixo - custoVariavel;

        // Ranking de categorias: soma as saídas do mês por categoria (mesmo helper usado para os meses
        // anteriores) e agrega % da receita + comparação com a média dos 3 meses anteriores, extrapolando
        // o mês atual (parcial) para o mês cheio. Não depende da forma interna do DRE (dre.GruposDespesa).
        var mapaGrupoRanking = categorias is { Count: > 0 }
            ? categorias.Where(c => !string.IsNullOrWhiteSpace(c.Grupo))
                .ToDictionary(c => c.Nome, c => c.Grupo!, StringComparer.OrdinalIgnoreCase)
            : _mapaGrupo;

        var totalMesAtual = SomarSaidasPorCategoria(doMesAtual);
        var mesesAnteriores = Enumerable.Range(1, 3)
            .Select(i => hoje.AddMonths(-i))
            .Select(m => SomarSaidasPorCategoria(registros.Where(r => r.Data.Year == m.Year && r.Data.Month == m.Month)))
            .ToList();

        var diasNoMes = DateTime.DaysInMonth(hoje.Year, hoje.Month);
        var ranking = totalMesAtual
            .Select(kv => (Grupo: mapaGrupoRanking.TryGetValue(kv.Key, out var g) ? g : "Outros", Nome: kv.Key, Total: kv.Value))
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

        // ── Indicadores de decisão ──────────────────────────────────────────
        var pontoEquilibrio = CalcularPontoEquilibrio(dre, hoje);

        // Série mensal de Custo Fixo ÷ Receita (últimos 6 meses) — mesma DRE por mês, reaproveitada
        // tanto pra tendência (item 4) quanto pro custo fixo médio do Fôlego de Caixa (item 2).
        var custoFixoMensal = Enumerable.Range(0, 6)
            .Select(i => hoje.AddMonths(-(5 - i)))
            .Select(m =>
            {
                var doMes = registros.Where(r => r.Data.Year == m.Year && r.Data.Month == m.Month).ToList();
                var dreMes = CalcularDre(doMes, categorias);
                return new CustoFixoMensalDto
                {
                    Mes = $"{m.Year}-{m.Month:D2}",
                    Receita = dreMes.ReceitaBruta,
                    CustoFixo = dreMes.DespesasFixas.Total,
                    Percentual = dreMes.ReceitaBruta > 0 ? Math.Round(dreMes.DespesasFixas.Total / dreMes.ReceitaBruta * 100, 1) : null,
                };
            })
            .ToList();

        var folegoCaixa = CalcularFolegoCaixa(registros, custoFixoMensal);
        var prazoRecebimento = CalcularPrazoRecebimento(registros);

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
            PontoEquilibrio = pontoEquilibrio,
            FolegoCaixa = folegoCaixa,
            CustoFixoMensal = custoFixoMensal,
            PrazoRecebimento = prazoRecebimento,
        };
    }

    // ── 1. Ponto de Equilíbrio ───────────────────────────────────────────────
    // Reaproveita a Margem de Contribuição já calculada no DRE (ReceitaLíquida − Custos Variáveis).
    private static PontoEquilibrioDetalhadoDto CalcularPontoEquilibrio(DreDto dre, DateOnly hoje)
    {
        if (dre.ReceitaBruta <= 0)
            return new PontoEquilibrioDetalhadoDto
            {
                Disponivel = false,
                MotivoIndisponivel = "Sem faturamento neste mês ainda — não é possível calcular o ponto de equilíbrio.",
                ReceitaAtual = dre.ReceitaBruta,
            };

        var margemPercentual = dre.MargemContribuicao / dre.ReceitaBruta;
        if (margemPercentual <= 0)
            return new PontoEquilibrioDetalhadoDto
            {
                Disponivel = false,
                MotivoIndisponivel = "Os custos variáveis (e deduções) consomem toda a receita — não sobra margem de contribuição para cobrir as despesas fixas. Rever preço ou custo variável antes de calcular o ponto de equilíbrio.",
                ReceitaAtual = dre.ReceitaBruta,
            };

        var valorMensal = dre.DespesasFixas.Total / margemPercentual;
        var diasUteis = ContarDiasUteis(hoje.Year, hoje.Month);
        var distancia = dre.ReceitaBruta - valorMensal;

        return new PontoEquilibrioDetalhadoDto
        {
            Disponivel = true,
            ValorMensal = Math.Round(valorMensal, 2),
            ValorPorDiaUtil = diasUteis > 0 ? Math.Round(valorMensal / diasUteis, 2) : null,
            DiasUteisNoMes = diasUteis,
            ReceitaAtual = dre.ReceitaBruta,
            Distancia = Math.Round(distancia, 2),
            DistanciaPercentual = valorMensal > 0 ? Math.Round(distancia / valorMensal * 100, 1) : null,
        };
    }

    private static int ContarDiasUteis(int ano, int mes)
    {
        var diasNoMes = DateTime.DaysInMonth(ano, mes);
        return Enumerable.Range(1, diasNoMes)
            .Select(d => new DateOnly(ano, mes, d).DayOfWeek)
            .Count(dow => dow != DayOfWeek.Saturday && dow != DayOfWeek.Sunday);
    }

    // ── 2. Fôlego de Caixa ───────────────────────────────────────────────────
    // Saldo consolidado (mesmo padrão de "todas as contas" do ProjecaoService: soma o SaldoFinal
    // mais recente de cada conta) ÷ custo fixo médio dos meses com atividade nos últimos 6.
    private static FolegoCaixaDto CalcularFolegoCaixa(List<RegistroDiario> registros, List<CustoFixoMensalDto> custoFixoMensal)
    {
        var saldoDisponivel = registros
            .Where(r => r.ContaBancariaId.HasValue)
            .GroupBy(r => r.ContaBancariaId)
            .Sum(g => g.OrderByDescending(r => r.Data).First().SaldoFinal);

        var mesesComCusto = custoFixoMensal.Where(m => m.Receita > 0 || m.CustoFixo > 0).ToList();
        if (mesesComCusto.Count == 0)
            return new FolegoCaixaDto
            {
                Disponivel = false,
                MotivoIndisponivel = "Sem despesas fixas registradas nos últimos meses — não é possível calcular o fôlego de caixa.",
                SaldoDisponivel = Math.Round(saldoDisponivel, 2),
            };

        var custoFixoMedio = mesesComCusto.Average(m => m.CustoFixo);
        if (custoFixoMedio <= 0)
            return new FolegoCaixaDto
            {
                Disponivel = false,
                MotivoIndisponivel = "Custo fixo médio dos últimos meses é zero — não é possível calcular o fôlego de caixa.",
                SaldoDisponivel = Math.Round(saldoDisponivel, 2),
            };

        var meses = saldoDisponivel / custoFixoMedio;
        var faixa = meses < 1 ? "critico" : meses <= 3 ? "atencao" : "confortavel";

        return new FolegoCaixaDto
        {
            Disponivel = true,
            SaldoDisponivel = Math.Round(saldoDisponivel, 2),
            CustoFixoMedioMensal = Math.Round(custoFixoMedio, 2),
            Meses = Math.Round(meses, 1),
            Faixa = faixa,
        };
    }

    // ── 5. Prazo Médio de Recebimento ────────────────────────────────────────
    // Diferença entre data de baixa e data prevista das contas a receber já baixadas.
    private static PrazoRecebimentoDto CalcularPrazoRecebimento(List<RegistroDiario> registros)
    {
        var amostras = registros
            .SelectMany(r => r.ContasReceber)
            .Where(c => c.Pago && c.DataBaixa.HasValue && c.DataVencimento.HasValue)
            .Select(c => c.DataBaixa!.Value.DayNumber - c.DataVencimento!.Value.DayNumber)
            .ToList();

        if (amostras.Count < 5)
            return new PrazoRecebimentoDto
            {
                Disponivel = false,
                MotivoIndisponivel = "Poucas contas a receber com data de vencimento e baixa registradas — dado insuficiente para um prazo médio confiável.",
                QuantidadeAmostras = amostras.Count,
            };

        var pctMesmoDia = amostras.Count(d => d == 0) / (decimal)amostras.Count;
        if (pctMesmoDia >= 0.8m)
            return new PrazoRecebimentoDto
            {
                Disponivel = false,
                MotivoIndisponivel = "A maioria das contas a receber foi baixada na mesma data prevista — comum quando o recebimento só é registrado no dia em que o dinheiro entra. Esse indicador não tem sinal útil aqui.",
                QuantidadeAmostras = amostras.Count,
            };

        return new PrazoRecebimentoDto
        {
            Disponivel = true,
            MediaDias = Math.Round((decimal)amostras.Average(), 1),
            QuantidadeAmostras = amostras.Count,
        };
    }

    private static Dictionary<string, decimal> SomarSaidasPorCategoria(IEnumerable<RegistroDiario> registros)
    {
        var dict = new Dictionary<string, decimal>();
        foreach (var s in registros.SelectMany(r => r.Saidas).Where(s => LancamentoFiltro.EhOperacional(s.TipoCusto)))
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
