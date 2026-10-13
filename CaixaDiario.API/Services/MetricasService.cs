using CaixaDiario.API.DTOs.Metricas;
using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

public class MetricasService : IMetricasService
{
    public MetricasPeriodoDto CalcularPeriodo(
        List<RegistroDiario> todosRegistros, List<RegistroDiario> registrosDoPeriodo, decimal saldoConsolidado,
        decimal multiplo = 3m, IReadOnlyList<Categoria>? categorias = null)
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

        var saldoAtual = saldoConsolidado;
        var totalReceber = todosRegistros.SelectMany(r => r.ContasReceber).Where(c => !c.Pago).Sum(c => c.Valor);
        var totalPagar = todosRegistros.SelectMany(r => r.ContasPagar).Where(c => !c.Pago).Sum(c => c.Valor);
        dto.SaldoProjetado = saldoAtual + totalReceber - totalPagar;

        // Valuation
        var ultimos3Meses = Enumerable.Range(0, 3)
            .Select(i => DataLocalHelper.Hoje().AddMonths(-i))
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

        // Runway — item 2 da rodada "Visão de Futuro": precisa dar o MESMO número que Indicadores >
        // Fôlego de Caixa pro mesmo saldo. Antes, cada um calculava o "custo fixo médio" de um jeito
        // (aqui: 3 meses, soma bruta por TipoCusto; lá: 6 meses só com atividade, DespesasFixas do
        // DRE por categoria) — convergiam o saldo (PR #43) mas nunca o divisor. Agora os dois chamam
        // CalcularCustoFixoMensal/CalcularFolegoCaixa — única fonte de verdade pro custo fixo médio.
        var custoFixoMensalRunway = CalcularCustoFixoMensal(todosRegistros, categorias, DataLocalHelper.Hoje());
        var folegoCaixaRunway = CalcularFolegoCaixa(saldoAtual, custoFixoMensalRunway);
        dto.BurnRate = folegoCaixaRunway.CustoFixoMedioMensal;
        var runwayMeses = folegoCaixaRunway.Meses ?? 0;

        dto.Runway = new RunwayDto
        {
            Meses = runwayMeses,
            Semaforo = !folegoCaixaRunway.Disponivel ? "cinza"
                : runwayMeses > 6 ? "verde"
                : runwayMeses >= 3 ? "amarelo"
                : "vermelho",
        };

        // Liquidez
        var hoje30 = DataLocalHelper.Hoje();
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

    public List<EvolucaoMensalDto> CalcularEvolucao(List<RegistroDiario> registros, int meses, DateOnly? ateMesReferencia = null)
    {
        var hoje = ateMesReferencia ?? DataLocalHelper.Hoje();
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
        var entradasOperacionais = registros.SelectMany(r => r.Entradas).Where(e => LancamentoFiltro.EhOperacional(e.TipoCusto)).ToList();
        // Entrada sem categoria (ainda pendente de classificação) não é receita confirmada — fica de
        // fora da Receita Bruta e entra, negativa, na linha "Não Classificados" (valor líquido junto
        // com as saídas sem categoria). Sem isso esse dinheiro contaria a mais no Resultado Líquido:
        // uma vez cheio na Receita Bruta, e nenhuma outra linha o desconta.
        var entradasNaoClassificadasTotal = entradasOperacionais
            .Where(e => string.IsNullOrWhiteSpace(e.Categoria))
            .Sum(e => e.Valor);
        var receitaBruta = entradasOperacionais.Sum(e => e.Valor) - entradasNaoClassificadasTotal;

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
        // Nome da categoria → Bloco do grupo (só populado quando o grupo tem Bloco definido — planos
        // de contas migrados pra hierarquia nova). Tem prioridade sobre o mapaGrupo/mapaTipo abaixo.
        Dictionary<string, string> mapaBloco = new(StringComparer.OrdinalIgnoreCase);

        if (categorias is { Count: > 0 })
        {
            mapaGrupo = categorias
                .Where(c => !string.IsNullOrWhiteSpace(c.Grupo?.Nome))
                .ToDictionary(c => c.Nome, c => c.Grupo!.Nome, StringComparer.OrdinalIgnoreCase);
            mapaOrdem = categorias.ToDictionary(c => c.Nome, c => c.Ordem, StringComparer.OrdinalIgnoreCase);
            mapaTipo = categorias.ToDictionary(c => c.Nome, c => c.Tipo, StringComparer.OrdinalIgnoreCase);
            mapaBloco = categorias
                .Where(c => !string.IsNullOrWhiteSpace(c.Grupo?.Bloco))
                .ToDictionary(c => c.Nome, c => c.Grupo!.Bloco, StringComparer.OrdinalIgnoreCase);
            ordemGrupos = categorias
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
        // Correção de integridade (Parte 1, item 6): Pró-labore/retiradas dos sócios — nunca foi
        // despesa fixa nem atividade de investimento/financiamento; é a última linha antes do
        // Resultado Líquido (decisão explícita do usuário), nunca "Não classificado".
        var retiradasSociosCats = new Dictionary<string, decimal>();

        foreach (var saida in saidasOperacionais)
        {
            var nomeCat = string.IsNullOrWhiteSpace(saida.Categoria) ? "Não Classificado" : saida.Categoria;
            var grupo = mapaGrupo.TryGetValue(saida.Categoria ?? "", out var g) ? g : null;
            var bloco = mapaBloco.TryGetValue(saida.Categoria ?? "", out var b) ? b : null;
            var tipo = mapaTipo != null
                ? (mapaTipo.TryGetValue(saida.Categoria ?? "", out var t) ? t : null)
                : saida.TipoCusto;

            // Bloco (Plano de Contas migrado pra hierarquia nova) manda quando disponível; senão
            // cai no critério histórico por Tipo + nome de grupo "Impostos".
            var bucket = bloco == Blocos.DeducoesDaReceita ? deducoesCats
                : bloco == Blocos.CustosOperacionais ? custosVariaveisCats
                : bloco == Blocos.DespesasOperacionais ? despesasFixasCats
                : bloco == Blocos.RetiradasSocios ? retiradasSociosCats
                : bloco != null ? naoClassificadoCats
                : string.Equals(grupo, "Impostos", StringComparison.OrdinalIgnoreCase) ? deducoesCats
                : tipo == "CustoVariavel" ? custosVariaveisCats
                : tipo == "CustoFixo" ? despesasFixasCats
                : tipo == "DespesaNaoOperacional" ? despesasNaoOperacionaisCats
                : tipo == "RetiradaSocios" ? retiradasSociosCats
                : naoClassificadoCats;

            bucket[nomeCat] = (bucket.TryGetValue(nomeCat, out var v) ? v : 0m) + saida.Valor;
        }

        // Entrada sem categoria entra NEGATIVA no mesmo bucket "Não Classificado" — a linha vira o
        // valor líquido (saídas sem categoria − entradas sem categoria), e o valor já foi retirado
        // da Receita Bruta acima (ver entradasNaoClassificadasTotal), então não é contado duas vezes.
        foreach (var entrada in entradasOperacionais.Where(e => string.IsNullOrWhiteSpace(e.Categoria)))
        {
            const string nomeCat = "Não Classificado";
            naoClassificadoCats[nomeCat] = (naoClassificadoCats.TryGetValue(nomeCat, out var v) ? v : 0m) - entrada.Valor;
        }

        // ---- Atividades de Investimento / Financiamento: fato modificativo pós-operacional ----
        // Mesmo padrão do Rendimento acima: nunca passam pelo filtro EhOperacional (excluídas de
        // saidasOperacionais/entradas), e o sinal vem de ser entrada (+) ou saída (-), não do Tipo —
        // uma categoria de investimento pode ser captação (entrada) ou aquisição (saída).
        DreLinhaVerticalDto MontarLinhaAtividade(string tipoCusto)
        {
            var cats = new Dictionary<string, decimal>();
            foreach (var entrada in registros.SelectMany(r => r.Entradas).Where(e => e.TipoCusto == tipoCusto))
            {
                var nome = string.IsNullOrWhiteSpace(entrada.Categoria) ? "Não Classificado" : entrada.Categoria;
                cats[nome] = (cats.TryGetValue(nome, out var v) ? v : 0m) + entrada.Valor;
            }
            foreach (var saida in registros.SelectMany(r => r.Saidas).Where(s => s.TipoCusto == tipoCusto))
            {
                var nome = string.IsNullOrWhiteSpace(saida.Categoria) ? "Não Classificado" : saida.Categoria;
                cats[nome] = (cats.TryGetValue(nome, out var v) ? v : 0m) - saida.Valor;
            }
            var total = cats.Values.Sum();
            return new DreLinhaVerticalDto
            {
                Total = total,
                Percentual = receitaBruta > 0 ? Math.Round(total / receitaBruta * 100, 1) : null,
                Categorias = cats
                    .OrderByDescending(kv => Math.Abs(kv.Value))
                    .Select(kv => new DreCategoriaDto
                    {
                        Nome = kv.Key, Total = kv.Value,
                        Percentual = receitaBruta > 0 ? Math.Round(kv.Value / receitaBruta * 100, 1) : null,
                    })
                    .ToList(),
            };
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
        var atividadesInvestimento = MontarLinhaAtividade(LancamentoFiltro.TipoInvestimento);
        var atividadesFinanciamento = MontarLinhaAtividade(LancamentoFiltro.TipoFinanciamento);
        // Pró-labore/retiradas dos sócios: decisão explícita do usuário — última dedução antes do
        // Resultado Líquido, depois de Investimento e Financiamento (não é despesa fixa nem atividade).
        var retiradasSocios = MontarLinhaVertical(retiradasSociosCats);
        var resultadoLiquido = resultadoOperacional + receitaFinanceira.Total - despesasNaoOperacionais.Total
            - naoClassificado.Total + atividadesInvestimento.Total + atividadesFinanciamento.Total - retiradasSocios.Total;

        var blocos = MontarBlocos(
            entradasOperacionais, saidasOperacionais, receitaBruta, mapaBloco, mapaGrupo, mapaOrdem,
            receitaFinanceiraCats, atividadesInvestimento, atividadesFinanciamento);

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
            AtividadesInvestimento = atividadesInvestimento,
            AtividadesFinanciamento = atividadesFinanciamento,
            RetiradasSocios = retiradasSocios,
            ResultadoLiquido = resultadoLiquido,
            ResultadoLiquidoPercentual = Percentual(resultadoLiquido),
            Blocos = blocos,
        };
    }

    // ── Estrutura hierárquica Bloco → Grupo → Categoria (demonstrativo de 3 níveis) ──────────────
    private static List<DreBlocoDto> MontarBlocos(
        List<ItemFinanceiro> entradasOperacionais, List<ItemFinanceiroSaida> saidasOperacionais, decimal receitaBruta,
        Dictionary<string, string> mapaBloco, Dictionary<string, string> mapaGrupo, Dictionary<string, int>? mapaOrdem,
        Dictionary<string, decimal> receitaFinanceiraCats,
        DreLinhaVerticalDto atividadesInvestimento, DreLinhaVerticalDto atividadesFinanciamento)
    {
        decimal? Percentual(decimal valor) => receitaBruta > 0 ? Math.Round(valor / receitaBruta * 100, 1) : null;
        const string semGrupo = "Não Classificado";

        // grupo -> categoria -> total, por bloco
        var porBloco = Blocos.Ordem.ToDictionary(b => b, _ => new Dictionary<string, Dictionary<string, decimal>>());

        void Lancar(string bloco, string? nomeCategoria, decimal valorComSinal)
        {
            var grupo = mapaGrupo.TryGetValue(nomeCategoria ?? "", out var g) ? g : semGrupo;
            var cat = string.IsNullOrWhiteSpace(nomeCategoria) ? "Não Classificado" : nomeCategoria;
            var porGrupo = porBloco[bloco];
            if (!porGrupo.TryGetValue(grupo, out var cats)) { cats = new(); porGrupo[grupo] = cats; }
            cats[cat] = (cats.TryGetValue(cat, out var v) ? v : 0m) + valorComSinal;
        }

        // Entradas operacionais já vêm sem Transferência/Rendimento/Investimento/Financiamento
        // (excluídos por EhOperacional antes daqui) — o que sobra é sempre Receita Operacional,
        // EXCETO a sem categoria: essa não é receita confirmada (mesma regra de receitaBruta
        // acima) — entra como crédito dentro do mesmo bucket "Não Classificado" de Despesas
        // Operacionais que as saídas sem categoria, pra "Não classificados" virar uma única linha
        // líquida (saídas − entradas) em vez de inflar Receitas Operacionais e mostrar só o bruto
        // das saídas na outra ponta — item A3 do bundle D6JWEAJa.
        foreach (var entrada in entradasOperacionais)
        {
            if (string.IsNullOrWhiteSpace(entrada.Categoria))
            {
                Lancar(Blocos.DespesasOperacionais, null, entrada.Valor);
                continue;
            }
            Lancar(Blocos.ReceitasOperacionais, entrada.Categoria, entrada.Valor);
        }
        foreach (var saida in saidasOperacionais)
        {
            var bloco = mapaBloco.TryGetValue(saida.Categoria ?? "", out var b) ? b
                : string.Equals(mapaGrupo.TryGetValue(saida.Categoria ?? "", out var g0) ? g0 : null, "Impostos", StringComparison.OrdinalIgnoreCase)
                    ? Blocos.DeducoesDaReceita
                    : Blocos.DespesasOperacionais;
            Lancar(bloco, saida.Categoria, -saida.Valor);
        }
        // Rendimento de investimento entra como grupo dedicado dentro de Atividades de Investimento —
        // mesma fonte de dado da linha ReceitaFinanceira, só reorganizada na árvore hierárquica.
        foreach (var (nome, valor) in receitaFinanceiraCats)
        {
            var porGrupo = porBloco[Blocos.AtividadesDeInvestimento];
            const string grupoRendimento = "Rendimento de Investimentos";
            if (!porGrupo.TryGetValue(grupoRendimento, out var cats)) { cats = new(); porGrupo[grupoRendimento] = cats; }
            cats[nome] = (cats.TryGetValue(nome, out var v) ? v : 0m) + valor;
        }
        foreach (var cat in atividadesInvestimento.Categorias)
            Lancar(Blocos.AtividadesDeInvestimento, cat.Nome, cat.Total);
        foreach (var cat in atividadesFinanciamento.Categorias)
            Lancar(Blocos.AtividadesDeFinanciamento, cat.Nome, cat.Total);

        // Todos os 6 blocos sempre aparecem, mesmo com total zero — a estrutura do demonstrativo
        // é fixa, não varia por período (evita a tela "pular" linhas de um mês pro outro).
        var resultado = new List<DreBlocoDto>();
        foreach (var bloco in Blocos.Ordem)
        {
            var gruposDoBloco = porBloco[bloco];

            var grupos = gruposDoBloco
                .OrderBy(kv => mapaOrdem != null && kv.Value.Keys.Any(mapaOrdem.ContainsKey)
                    ? kv.Value.Keys.Where(mapaOrdem.ContainsKey).Min(k => mapaOrdem[k]) : int.MaxValue)
                .ThenByDescending(kv => Math.Abs(kv.Value.Values.Sum()))
                .Select(kv =>
                {
                    var categorias = kv.Value
                        .OrderBy(c => mapaOrdem != null && mapaOrdem.TryGetValue(c.Key, out var o) ? o : int.MaxValue)
                        .ThenByDescending(c => Math.Abs(c.Value))
                        .Select(c => new DreCategoriaDto { Nome = c.Key, Total = c.Value, Percentual = Percentual(c.Value) })
                        .ToList();
                    var totalGrupo = categorias.Sum(c => c.Total);
                    return new DreGrupoDto { Nome = kv.Key, Total = totalGrupo, Percentual = Percentual(totalGrupo), Categorias = categorias };
                })
                .ToList();

            var totalBloco = grupos.Sum(g => g.Total);
            resultado.Add(new DreBlocoDto { Bloco = bloco, Total = totalBloco, Percentual = Percentual(totalBloco), Grupos = grupos });
        }
        return resultado;
    }

    public IndicadoresDecisaoDto CalcularIndicadores(
        List<RegistroDiario> registros, int mesesEvolucao = 13, IReadOnlyList<Categoria>? categorias = null,
        decimal saldoDisponivel = 0m, DateOnly? mesReferencia = null)
    {
        var hoje = DataLocalHelper.Hoje();
        // mesRef é o mês REALMENTE exibido (o atual, ou o último com dados quando o front detecta
        // que o atual está vazio — item 10 do Bloco 5). ehMesAtual gateia a extrapolação/comparação
        // "mesmos N dias": só fazem sentido pra um mês em andamento, nunca pra um mês passado fechado.
        var mesRef = mesReferencia ?? hoje;
        var ehMesAtual = mesRef.Year == hoje.Year && mesRef.Month == hoje.Month;
        var doMesAtual = registros.Where(r => r.Data.Year == mesRef.Year && r.Data.Month == mesRef.Month).ToList();

        var dre = CalcularDre(doMesAtual, categorias);
        var evolucao = CalcularEvolucao(registros, mesesEvolucao, mesRef);

        // Fixo x Variável x Não Classificado — sempre reconcilia com dre.TotalDespesas
        var saidasMes = doMesAtual.SelectMany(r => r.Saidas).Where(s => LancamentoFiltro.EhOperacional(s.TipoCusto)).ToList();
        var custoFixo = saidasMes.Where(s => s.TipoCusto == "CustoFixo").Sum(s => s.Valor);
        var custoVariavel = saidasMes.Where(s => s.TipoCusto == "CustoVariavel").Sum(s => s.Valor);
        var custoNaoClassificado = dre.TotalDespesas - custoFixo - custoVariavel;

        // Ranking de categorias: soma as saídas do mês por categoria (mesmo helper usado para os meses
        // anteriores) e agrega % da receita + comparação com a média dos 3 meses anteriores, extrapolando
        // o mês atual (parcial) para o mês cheio. Não depende da forma interna do DRE (dre.GruposDespesa).
        var mapaGrupoRanking = categorias is { Count: > 0 }
            ? categorias.Where(c => !string.IsNullOrWhiteSpace(c.Grupo?.Nome))
                .ToDictionary(c => c.Nome, c => c.Grupo!.Nome, StringComparer.OrdinalIgnoreCase)
            : _mapaGrupo;

        var totalMesAtual = SomarSaidasPorCategoria(doMesAtual);
        var mesesAnteriores = Enumerable.Range(1, 3)
            .Select(i => mesRef.AddMonths(-i))
            .Select(m => SomarSaidasPorCategoria(registros.Where(r => r.Data.Year == m.Year && r.Data.Month == m.Month)))
            .ToList();

        var diasNoMes = DateTime.DaysInMonth(mesRef.Year, mesRef.Month);
        var ranking = totalMesAtual
            .Select(kv => (Grupo: mapaGrupoRanking.TryGetValue(kv.Key, out var g) ? g : "Outros", Nome: kv.Key, Total: kv.Value))
            .Select(c =>
            {
                var mediasEncontradas = mesesAnteriores.Select(m => m.TryGetValue(c.Nome, out var v) ? v : 0m).ToList();
                var media = mediasEncontradas.Any(v => v > 0) ? mediasEncontradas.Average() : (decimal?)null;
                // Só extrapola quando mesRef é o mês em andamento de verdade — um mês de
                // referência passado (fallback do item 10) já está fechado, com todos os dias.
                var extrapolado = ehMesAtual && mesRef.Day > 0 ? c.Total / mesRef.Day * diasNoMes : c.Total;
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
            decimal receitaAnteriorComparavel;
            if (ehMesAtual)
            {
                // Item 5: o mês atual está em andamento, só com os primeiros N dias lançados.
                // Comparar isso com o mês anterior INTEIRO (fechado) sempre mostra uma queda
                // artificial, mesmo num mês bom. Recorta o mês anterior nos mesmos N dias (mesma
                // ideia já usada no Dashboard — ver utils/periodo.ts no frontend, Item 1.7).
                var mesAnteriorRef = mesRef.AddMonths(-1);
                var ateAnteriorMesmosDias = new DateOnly(mesAnteriorRef.Year, mesAnteriorRef.Month,
                    Math.Min(hoje.Day, DateTime.DaysInMonth(mesAnteriorRef.Year, mesAnteriorRef.Month)));
                receitaAnteriorComparavel = registros
                    .Where(r => r.Data.Year == mesAnteriorRef.Year && r.Data.Month == mesAnteriorRef.Month && r.Data <= ateAnteriorMesmosDias)
                    .SelectMany(r => r.Entradas)
                    .Where(e => LancamentoFiltro.EhOperacional(e.TipoCusto))
                    .Sum(e => e.Valor);
            }
            else
            {
                // mesRef já é um mês fechado (fallback do item 10) — os dois meses são comparáveis
                // por inteiro, sem precisar recortar dias.
                receitaAnteriorComparavel = evolucao[^2].Receita;
            }
            if (receitaAnteriorComparavel > 0)
                variacaoMoM = Math.Round((atual.Receita - receitaAnteriorComparavel) / receitaAnteriorComparavel * 100, 1);
        }
        if (evolucao.Count >= 13)
        {
            var atual = evolucao[^1];
            var mesmoMesAnoAnterior = evolucao[^13];
            if (mesmoMesAnoAnterior.Receita > 0)
                variacaoYoY = Math.Round((atual.Receita - mesmoMesAnoAnterior.Receita) / mesmoMesAnoAnterior.Receita * 100, 1);
        }

        // ── Indicadores de decisão ──────────────────────────────────────────
        var pontoEquilibrio = CalcularPontoEquilibrio(dre.ReceitaBruta, dre.MargemContribuicao, dre.DespesasFixas.Total, mesRef);

        // Série mensal de Custo Fixo ÷ Receita (últimos 6 meses) — mesma DRE por mês, reaproveitada
        // tanto pra tendência (item 4) quanto pro custo fixo médio do Fôlego de Caixa (item 2) e do
        // Runway do Dashboard (CalcularPeriodo) — única fonte de verdade pro custo fixo médio.
        var custoFixoMensal = CalcularCustoFixoMensal(registros, categorias, mesRef);

        var folegoCaixa = CalcularFolegoCaixa(saldoDisponivel, custoFixoMensal);
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
    // Recebe as peças já prontas (não um DreDto inteiro) pra poder ser chamado tanto com o DRE de
    // um mês civil (Indicadores de Decisão) quanto com o DRE de qualquer período arbitrário
    // (painel de KPIs da tela de DRE — mês, trimestre ou ano). `diaReferencia` só define o mês
    // usado pro detalhamento "por dia útil", que só faz sentido pra período mensal.
    public PontoEquilibrioDetalhadoDto CalcularPontoEquilibrio(
        decimal receitaBruta, decimal margemContribuicao, decimal despesasFixasTotal, DateOnly diaReferencia)
    {
        if (receitaBruta <= 0)
            return new PontoEquilibrioDetalhadoDto
            {
                Disponivel = false,
                MotivoIndisponivel = "Sem faturamento neste período ainda — não é possível calcular o ponto de equilíbrio.",
                ReceitaAtual = receitaBruta,
            };

        var margemPercentual = margemContribuicao / receitaBruta;
        if (margemPercentual <= 0)
            return new PontoEquilibrioDetalhadoDto
            {
                Disponivel = false,
                MotivoIndisponivel = "Os custos variáveis (e deduções) consomem toda a receita — não sobra margem de contribuição para cobrir as despesas fixas. Rever preço ou custo variável antes de calcular o ponto de equilíbrio.",
                ReceitaAtual = receitaBruta,
            };

        var valorMensal = despesasFixasTotal / margemPercentual;
        var diasUteis = ContarDiasUteis(diaReferencia.Year, diaReferencia.Month);
        var distancia = receitaBruta - valorMensal;

        return new PontoEquilibrioDetalhadoDto
        {
            Disponivel = true,
            ValorMensal = Math.Round(valorMensal, 2),
            ValorPorDiaUtil = diasUteis > 0 ? Math.Round(valorMensal / diasUteis, 2) : null,
            DiasUteisNoMes = diasUteis,
            ReceitaAtual = receitaBruta,
            Distancia = Math.Round(distancia, 2),
            DistanciaPercentual = valorMensal > 0 ? Math.Round(distancia / valorMensal * 100, 1) : null,
        };
    }

    // ── 3. Evolução do Resultado Líquido (sparkline, painel de KPIs da DRE) ──────────────────
    // Mesmo padrão do custoFixoMensal acima (chama CalcularDre mês a mês), só que ancorado no
    // último mês do período selecionado na tela de DRE — não em "hoje" — pra continuar coerente
    // quando o usuário está olhando um período passado.
    public List<ResultadoLiquidoMensalDto> CalcularResultadoLiquidoMensal(
        List<RegistroDiario> registros, DateOnly ateMesReferencia, IReadOnlyList<Categoria>? categorias, int meses = 6)
    {
        return Enumerable.Range(0, meses)
            .Select(i => ateMesReferencia.AddMonths(-(meses - 1 - i)))
            .Select(m =>
            {
                var doMes = registros.Where(r => r.Data.Year == m.Year && r.Data.Month == m.Month && !r.Excluido).ToList();
                var dreMes = CalcularDre(doMes, categorias);
                return new ResultadoLiquidoMensalDto
                {
                    Mes = $"{m.Year}-{m.Month:D2}",
                    ResultadoLiquido = dreMes.ResultadoLiquido,
                    ResultadoLiquidoPercentual = dreMes.ResultadoLiquidoPercentual,
                    TemDados = dreMes.ReceitaBruta > 0 || dreMes.TotalDespesas > 0,
                };
            })
            .ToList();
    }

    // Série mensal (6 meses) de Receita/CustoFixo via DRE por categoria — única fonte de verdade
    // do "custo fixo médio mensal", usada tanto pelo Fôlego de Caixa (Indicadores) quanto pelo
    // Runway (Dashboard, CalcularPeriodo), pra nunca mais divergirem pro mesmo saldo.
    private List<CustoFixoMensalDto> CalcularCustoFixoMensal(
        List<RegistroDiario> registros, IReadOnlyList<Categoria>? categorias, DateOnly hoje)
    {
        return Enumerable.Range(0, 6)
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
    }

    private static int ContarDiasUteis(int ano, int mes)
    {
        var diasNoMes = DateTime.DaysInMonth(ano, mes);
        return Enumerable.Range(1, diasNoMes)
            .Select(d => new DateOnly(ano, mes, d).DayOfWeek)
            .Count(dow => dow != DayOfWeek.Saturday && dow != DayOfWeek.Sunday);
    }

    // ── 2. Fôlego de Caixa ───────────────────────────────────────────────────
    // Item 1.5: saldoDisponivel vem de fora (mesma fórmula do MetricasController.
    // CalcularSaldoConsolidadoAsync usada no Runway do Dashboard — contas correntes + Caixa, sem
    // investimento) pra Dashboard e Indicadores nunca mais mostrarem números diferentes pro mesmo
    // conceito. ÷ custo fixo médio dos meses com atividade nos últimos 6.
    private static FolegoCaixaDto CalcularFolegoCaixa(decimal saldoDisponivel, List<CustoFixoMensalDto> custoFixoMensal)
    {
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

    // Indicadores — Parte 2, item 3 (Money Vision): alguns clientes usam a categoria "Cartão de
    // crédito" como apelido pra "paguei a fatura" em lançamentos manuais (TipoCusto genérico,
    // nunca PagamentoFatura de verdade) — sem isso ela aparecia como a maior "despesa" do
    // ranking, quando a despesa de cada compra já foi reconhecida lançamento a lançamento (ou vai
    // ser, quando a fatura do cartão for importada). Só afeta este ranking, não o DRE.
    private static bool EhPagamentoDeFaturaPorNome(string? categoria) =>
        string.Equals(categoria?.Trim(), "Cartão de crédito", StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, decimal> SomarSaidasPorCategoria(IEnumerable<RegistroDiario> registros)
    {
        var dict = new Dictionary<string, decimal>();
        foreach (var s in registros.SelectMany(r => r.Saidas)
            .Where(s => LancamentoFiltro.EhOperacional(s.TipoCusto) && !EhPagamentoDeFaturaPorNome(s.Categoria)))
        {
            var cat = string.IsNullOrWhiteSpace(s.Categoria) ? "Não Classificado" : s.Categoria;
            dict[cat] = (dict.TryGetValue(cat, out var v) ? v : 0m) + s.Valor;
        }
        return dict;
    }

    public ParaOndeFoiMeuDinheiroDto CalcularParaOndeFoiMeuDinheiro(List<RegistroDiario> registrosDoMes, HashSet<Guid> contasInvestimentoIds)
    {
        // Mesmo filtro EhOperacional do resto do DRE — nunca conta transferência, rendimento,
        // investimento ou pagamento de fatura como "entrou"/"gastou" de verdade.
        var entrou = registrosDoMes.SelectMany(r => r.Entradas).Where(e => LancamentoFiltro.EhOperacional(e.TipoCusto)).Sum(e => e.Valor);
        var gastou = registrosDoMes.SelectMany(r => r.Saidas).Where(s => LancamentoFiltro.EhOperacional(s.TipoCusto)).Sum(s => s.Valor);

        // "Guardou" é só o movimento de Transferencia que caiu numa conta de Investimento —
        // aplicação (entrada lá) soma, resgate (saída lá) subtrai. Transferência entre duas contas
        // "comuns" (ex.: corrente pra corrente) não é "guardar", por isso o filtro por conta.
        var guardou = registrosDoMes
            .Where(r => r.ContaBancariaId.HasValue && contasInvestimentoIds.Contains(r.ContaBancariaId.Value))
            .Sum(r =>
                r.Entradas.Where(e => e.TipoCusto == LancamentoFiltro.TipoTransferencia).Sum(e => e.Valor)
                - r.Saidas.Where(s => s.TipoCusto == LancamentoFiltro.TipoTransferencia).Sum(s => s.Valor));

        var ficou = entrou - gastou - guardou;

        return new ParaOndeFoiMeuDinheiroDto
        {
            Entrou = entrou,
            Gastou = gastou,
            Guardou = guardou,
            Ficou = ficou,
            TaxaPoupancaPercentual = entrou > 0 ? Math.Round(guardou / entrou * 100, 1) : null,
        };
    }
}
