using CaixaDiario.API.Models;
using CaixaDiario.API.Services;

namespace CaixaDiario.Tests.Services;

public class MetricasServiceTests
{
    private readonly MetricasService _sut = new();

    private static RegistroDiario CriarRegistro(
        DateOnly data, List<ItemFinanceiro> entradas, List<ItemFinanceiro> saidas, decimal saldoFinal = 0, Guid? contaBancariaId = null) =>
        new()
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Data = data, Entradas = entradas, ContaBancariaId = contaBancariaId,
            Saidas = saidas.Select(s => new ItemFinanceiroSaida { Descricao = s.Descricao, Valor = s.Valor, Categoria = s.Categoria ?? "", TipoCusto = s.TipoCusto }).ToList(),
            SaldoFinal = saldoFinal
        };

    private static ItemFinanceiro Item(string desc, decimal valor, string? categoria = null, string? tipoCusto = null) =>
        new() { Descricao = desc, Valor = valor, Categoria = categoria, TipoCusto = tipoCusto };

    // Bloco vazio ("") preserva o comportamento histórico dos testes que só conhecem Tipo/Grupo
    // por nome — o cálculo do DRE cai no critério antigo (Tipo + nome de grupo) quando Bloco não
    // está resolvido. Passe `bloco` explicitamente nos testes que exercitam a hierarquia nova.
    private static Categoria Cat(string nome, string tipo, string? grupo = null, int ordem = 0, string bloco = "") =>
        new() { Id = Guid.NewGuid(), Nome = nome, Tipo = tipo, GrupoId = Guid.NewGuid(), Grupo = new Grupo { Nome = grupo ?? "Outros", Bloco = bloco }, Ordem = ordem, Ativa = true };

    [Fact]
    public void CalcularPeriodo_SemCategorias_EbitdaNull()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m) },
            new() { Item("Aluguel", 300m) });
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg }, 0m);
        Assert.Null(resultado.Ebitda);
    }

    [Fact]
    public void CalcularPeriodo_ComCategorias_CalculaEbitdaCorreto()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new()
            {
                Item("Aluguel", 300m, "Aluguel", "CustoFixo"),
                Item("Insumos", 200m, "Insumos/Mercadoria", "CustoVariavel"),
            });
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg }, 0m);
        Assert.NotNull(resultado.Ebitda);
        Assert.Equal(500m, resultado.Ebitda!.Valor);
        Assert.Equal(0.5m, resultado.Ebitda.Percentual);
        Assert.Equal("verde", resultado.Ebitda.Semaforo);
    }

    [Fact]
    public void CalcularPeriodo_ManutencaoNaoEntraNoEbitda()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new() { Item("Manutenção", 200m, "Manutenção", "CustoFixo") });
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg }, 0m);
        Assert.NotNull(resultado.Ebitda);
        Assert.Equal(1000m, resultado.Ebitda!.Valor);
    }

    [Fact]
    public void CalcularPeriodo_SemSalariosEInsumos_PrimeCostNull()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new() { Item("Aluguel", 300m, "Aluguel", "CustoFixo") });
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg }, 0m);
        Assert.Null(resultado.PrimeCost);
    }

    [Fact]
    public void CalcularPeriodo_ComSalariosEInsumos_CalculaPrimeCostCorreto()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new()
            {
                Item("Salários", 400m, "Salários/Folha", "CustoFixo"),
                Item("Insumos", 300m, "Insumos/Mercadoria", "CustoVariavel"),
            });
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg }, 0m);
        Assert.NotNull(resultado.PrimeCost);
        Assert.Equal(0.7m, resultado.PrimeCost!.Percentual);
        Assert.Equal("amarelo", resultado.PrimeCost.Semaforo);
    }

    // Money Vision: "números que se contradizem" (item 1) — antes, o Dashboard excluía a
    // categoria "Manutenção" do custo fixo (uma regra só dela, nunca aplicada no DRE/Indicadores) e
    // usava receita BRUTA sem deduções. Com a fonte única (CalcularDre), o Ponto de Equilíbrio do
    // Dashboard tem que bater exatamente com o que CalcularDre/CalcularPontoEquilibrio dariam pro
    // mesmo período, "Manutenção" incluída.
    [Fact]
    public void CalcularPeriodo_ComCategoriaManutencaoEDeducao_PontoDeEquilibrioBateComODre()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new()
            {
                Item("Imposto", 50m, "Impostos", "CustoFixo"),
                Item("Manutenção predial", 100m, "Manutenção", "CustoFixo"),
                Item("Insumos", 200m, "Insumos/Mercadoria", "CustoVariavel"),
            });

        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg }, 0m);
        var dre = _sut.CalcularDre(new() { reg });
        var esperado = _sut.CalcularPontoEquilibrio(dre.ReceitaBruta, dre.MargemContribuicao, dre.DespesasFixas.Total, DataLocalHelper.Hoje());

        Assert.NotNull(resultado.PontoDeEquilibrio);
        Assert.Equal(esperado.ValorMensal, resultado.PontoDeEquilibrio!.Valor);
    }

    [Fact]
    public void CalcularPeriodo_SemCategoria_PontoDeEquilibrioNull()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m) },
            new() { Item("Aluguel", 300m) });
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg }, 0m);
        Assert.Null(resultado.PontoDeEquilibrio);
    }

    [Fact]
    public void CalcularPeriodo_ComCategorias_CalculaPontoDeEquilibrioCorreto()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new()
            {
                Item("Aluguel", 300m, "Aluguel", "CustoFixo"),
                Item("Insumos", 200m, "Insumos/Mercadoria", "CustoVariavel"),
            });
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg }, 0m);
        Assert.NotNull(resultado.PontoDeEquilibrio);
        Assert.Equal(375m, resultado.PontoDeEquilibrio!.Valor);
        Assert.Equal("verde", resultado.PontoDeEquilibrio.Semaforo);
    }

    [Fact]
    public void CalcularPeriodo_ReceitaZero_NaoDividePorZero()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1), new(), new());
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg }, 0m);
        Assert.Null(resultado.Ebitda);
        Assert.Null(resultado.PrimeCost);
        Assert.Null(resultado.PontoDeEquilibrio);
    }

    [Fact]
    public void CalcularEvolucao_RetornaMesesCorretos()
    {
        var hoje = DataLocalHelper.Hoje();
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(hoje.AddDays(-5),
                new() { Item("Venda", 1000m, "Vendas", "Receita") },
                new() { Item("Aluguel", 300m, "Aluguel", "CustoFixo") },
                saldoFinal: 5000m),
        };
        var resultado = _sut.CalcularEvolucao(registros, 3);
        Assert.Equal(3, resultado.Count);
        var mesAtual = resultado.Last();
        Assert.Equal(1000m, mesAtual.Receita);
        Assert.Equal(300m, mesAtual.Custos);
        Assert.Equal(700m, mesAtual.Lucro);
    }

    // ---- Valuation ----

    [Fact]
    public void CalcularPeriodo_TresMesesDeDados_CalculaValuation()
    {
        var hoje = DataLocalHelper.Hoje();
        var registros = new List<RegistroDiario>();
        for (int i = 2; i >= 0; i--)
        {
            var data = hoje.AddMonths(-i).AddDays(-5);
            registros.Add(CriarRegistro(data,
                new() { Item("Venda", 2000m, "Vendas", "Receita") },
                new() { Item("Custo", 1000m, "Aluguel", "CustoFixo") }));
        }

        var resultado = _sut.CalcularPeriodo(registros, registros, 0m);

        Assert.NotNull(resultado.Valuation);
        Assert.True(resultado.Valuation!.Valor > 0);
    }

    [Fact]
    public void CalcularPeriodo_SaldoZero_RunwayRetornaZero()
    {
        var hoje = DataLocalHelper.Hoje();
        var registro = CriarRegistro(hoje,
            new() { Item("Venda", 500m, "Vendas", "Receita") },
            new() { Item("Custo", 1000m, "Aluguel", "CustoFixo") },
            saldoFinal: 0m);

        var resultado = _sut.CalcularPeriodo(new() { registro }, new() { registro }, 0m);

        Assert.NotNull(resultado.Runway);
        Assert.Equal(0m, resultado.Runway!.Meses);
    }

    [Fact]
    public void CalcularPeriodo_SemContasPagarProximos30Dias_LiquidezAltaLiquidez()
    {
        var hoje = DataLocalHelper.Hoje();
        var registro = CriarRegistro(hoje, new(), new(), saldoFinal: 5000m);

        var resultado = _sut.CalcularPeriodo(new() { registro }, new() { registro }, 5000m);

        Assert.NotNull(resultado.Liquidez);
        Assert.True(resultado.Liquidez!.AltaLiquidez);
    }

    [Fact]
    public void CalcularPeriodo_ComContasPagarProximos30Dias_CalculaLiquidez()
    {
        var hoje = DataLocalHelper.Hoje();
        var amanha = hoje.AddDays(1);
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Data = hoje,
            Entradas = new(), Saidas = new(), SaldoFinal = 3000m,
            ContasReceber = new(),
            ContasPagar = new() { new() { Descricao = "Aluguel", Valor = 1000m, DataVencimento = amanha, Pago = false } },
        };

        var resultado = _sut.CalcularPeriodo(new() { registro }, new() { registro }, 3000m);

        Assert.NotNull(resultado.Liquidez);
        Assert.Equal(3.0m, resultado.Liquidez!.Indice);
        Assert.Equal("verde", resultado.Liquidez.Semaforo);
        Assert.False(resultado.Liquidez.AltaLiquidez);
    }

    [Fact]
    public void CalcularPeriodo_ComContasReceberProximos30Dias_SomaNoNumeradorLiquidez()
    {
        var hoje = DataLocalHelper.Hoje();
        var amanha = hoje.AddDays(1);
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Data = hoje,
            Entradas = new(), Saidas = new(), SaldoFinal = 1000m,
            ContasReceber = new() { new() { Descricao = "Cliente X", Valor = 2000m, DataVencimento = amanha, Pago = false } },
            ContasPagar = new() { new() { Descricao = "Aluguel", Valor = 1000m, DataVencimento = amanha, Pago = false } },
        };
        var resultado = _sut.CalcularPeriodo(new() { registro }, new() { registro }, 1000m);
        // (1000 + 2000) / 1000 = 3.0
        Assert.NotNull(resultado.Liquidez);
        Assert.Equal(3.0m, resultado.Liquidez!.Indice);
    }

    // ---- Ticket Médio ----

    [Fact]
    public void CalcularPeriodo_ComRecebimentos_CalculaTicketMedio()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda 1", 600m, "Vendas", "Receita"), Item("Venda 2", 400m, "Vendas", "Receita") },
            new());
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg }, 0m);
        Assert.NotNull(resultado.TicketMedio);
        Assert.Equal(2, resultado.TicketMedio!.QuantidadeRecebimentos);
        Assert.Equal(500m, resultado.TicketMedio.Valor);
    }

    [Fact]
    public void CalcularPeriodo_SemRecebimentos_TicketMedioNull()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1), new(), new() { Item("Aluguel", 300m, "Aluguel", "CustoFixo") });
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg }, 0m);
        Assert.Null(resultado.TicketMedio);
    }

    // ---- Múltiplo Valuation ----

    [Fact]
    public void CalcularPeriodo_MultiploCustomizado_AplicaNoValuation()
    {
        var hoje = DataLocalHelper.Hoje();
        var registros = new List<RegistroDiario>();
        for (int i = 2; i >= 0; i--)
            registros.Add(CriarRegistro(hoje.AddMonths(-i).AddDays(-5),
                new() { Item("Venda", 2000m, "Vendas", "Receita") },
                new() { Item("Custo", 1000m, "Aluguel", "CustoFixo") }));

        var v3 = _sut.CalcularPeriodo(registros, registros, 0m, 3m).Valuation!.Valor;
        var v6 = _sut.CalcularPeriodo(registros, registros, 0m, 6m).Valuation!.Valor;
        Assert.Equal(v3 * 2, v6);
    }

    // ---- Burn Rate ----

    [Fact]
    public void CalcularPeriodo_ComSaidas3Meses_CalculaBurnRate()
    {
        var hoje = DataLocalHelper.Hoje();
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(hoje, new() { Item("Venda", 100m, "Vendas", "Receita") },
                new() { Item("Custo", 900m, "Aluguel", "CustoFixo") }, saldoFinal: 1000m),
        };
        var resultado = _sut.CalcularPeriodo(registros, registros, 1000m);
        Assert.NotNull(resultado.BurnRate);
        Assert.Equal(900m, resultado.BurnRate);
    }

    // ---- CalcularIndicadores ----

    [Fact]
    public void CalcularIndicadores_FixoVariavelNaoClassificado_ReconciliaComTotalDespesas()
    {
        var hoje = DataLocalHelper.Hoje();
        var reg = CriarRegistro(hoje,
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new()
            {
                Item("Aluguel", 200m, "Aluguel", "CustoFixo"),
                Item("Insumos", 150m, "Insumos/Mercadoria", "CustoVariavel"),
                Item("Diversos", 50m, "Outros tributos", null),
            });

        var resultado = _sut.CalcularIndicadores(new() { reg });

        Assert.Equal(200m, resultado.CustoFixo);
        Assert.Equal(150m, resultado.CustoVariavel);
        Assert.Equal(50m, resultado.CustoNaoClassificado);
        Assert.Equal(resultado.Dre.TotalDespesas, resultado.CustoFixo + resultado.CustoVariavel + resultado.CustoNaoClassificado);
    }

    [Fact]
    public void CalcularIndicadores_RankingCategorias_CalculaPercentualSobreReceita()
    {
        var hoje = DataLocalHelper.Hoje();
        var reg = CriarRegistro(hoje,
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new() { Item("Aluguel", 200m, "Aluguel", "CustoFixo") });

        var resultado = _sut.CalcularIndicadores(new() { reg });

        var categoria = Assert.Single(resultado.RankingCategorias);
        Assert.Equal("Aluguel", categoria.Nome);
        Assert.Equal(200m, categoria.Total);
        Assert.Equal(20.0m, categoria.PercentualReceita);
    }

    [Fact]
    public void CalcularIndicadores_CategoriaCartaoDeCredito_NaoEntraNoRanking()
    {
        var hoje = DataLocalHelper.Hoje();
        var reg = CriarRegistro(hoje,
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new()
            {
                Item("Pagamento de fatura", 2000m, "Cartão de crédito", "CustoVariavel"),
                Item("Aluguel", 200m, "Aluguel", "CustoFixo"),
            });

        var resultado = _sut.CalcularIndicadores(new() { reg });

        Assert.DoesNotContain(resultado.RankingCategorias, c => c.Nome == "Cartão de crédito");
        var categoria = Assert.Single(resultado.RankingCategorias);
        Assert.Equal("Aluguel", categoria.Nome);
    }

    [Fact]
    public void CalcularIndicadores_CategoriaComHistoricoAnterior_CalculaMediaEVariacaoPositiva()
    {
        var hoje = DataLocalHelper.Hoje();
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(hoje, new(), new() { Item("Aluguel", 300m, "Aluguel", "CustoFixo") }),
        };
        for (int i = 1; i <= 3; i++)
            registros.Add(CriarRegistro(hoje.AddMonths(-i), new(), new() { Item("Aluguel", 100m, "Aluguel", "CustoFixo") }));

        var resultado = _sut.CalcularIndicadores(registros);

        var categoria = resultado.RankingCategorias.Single(c => c.Nome == "Aluguel");
        Assert.Equal(100m, categoria.MediaMesesAnteriores);
        Assert.NotNull(categoria.VariacaoPercentual);
        Assert.True(categoria.VariacaoPercentual > 0);
    }

    [Fact]
    public void CalcularIndicadores_CategoriaSemHistoricoAnterior_MediaEVariacaoNulas()
    {
        var hoje = DataLocalHelper.Hoje();
        var reg = CriarRegistro(hoje, new(), new() { Item("Software", 80m, "Software", "CustoFixo") });

        var resultado = _sut.CalcularIndicadores(new() { reg });

        var categoria = Assert.Single(resultado.RankingCategorias);
        Assert.Null(categoria.MediaMesesAnteriores);
        Assert.Null(categoria.VariacaoPercentual);
    }

    // ---- Item 10 do Bloco 5 ("Visão de Futuro"): mesReferencia (fallback de mês vazio) ----

    [Fact]
    public void CalcularIndicadores_ComMesReferenciaNoPassado_UsaOsDadosDesseMesEmVezDoAtual()
    {
        var hoje = DataLocalHelper.Hoje();
        var mesPassado = hoje.AddMonths(-2);
        var reg = CriarRegistro(mesPassado,
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new() { Item("Aluguel", 200m, "Aluguel", "CustoFixo") });
        // "hoje" não tem nenhum lançamento — mês atual vazio; só o mesPassado tem dado.

        var resultado = _sut.CalcularIndicadores(new() { reg }, mesReferencia: new DateOnly(mesPassado.Year, mesPassado.Month, 1));

        Assert.Equal(1000m, resultado.Dre.ReceitaBruta);
        var categoria = Assert.Single(resultado.RankingCategorias);
        Assert.Equal(200m, categoria.Total);
    }

    [Fact]
    public void CalcularIndicadores_ComMesReferenciaNoPassado_NaoExtrapolaORanking()
    {
        var hoje = DataLocalHelper.Hoje();
        var mesPassado = hoje.AddMonths(-2);
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(mesPassado, new(), new() { Item("Aluguel", 100m, "Aluguel", "CustoFixo") }),
        };
        for (int i = 1; i <= 3; i++)
            registros.Add(CriarRegistro(mesPassado.AddMonths(-i), new(), new() { Item("Aluguel", 100m, "Aluguel", "CustoFixo") }));

        var resultado = _sut.CalcularIndicadores(registros, mesReferencia: new DateOnly(mesPassado.Year, mesPassado.Month, 1));

        // Se extrapolasse (como faz pro mês em andamento), 100 ÷ dia-do-mesRef(=1) × dias-do-mês
        // infladaria o total muito acima dos 100 históricos — mesRef passado nunca extrapola.
        var categoria = resultado.RankingCategorias.Single(c => c.Nome == "Aluguel");
        Assert.Equal(100m, categoria.MediaMesesAnteriores);
        Assert.Equal(0m, categoria.VariacaoPercentual);
    }

    [Fact]
    public void CalcularIndicadores_ComMesReferenciaNoPassado_ComparaReceitaDosMesesInteiros()
    {
        var hoje = DataLocalHelper.Hoje();
        var mesPassado = hoje.AddMonths(-2);
        var mesAnterior = mesPassado.AddMonths(-1);
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(mesPassado, new() { Item("Venda", 1000m, "Vendas", "Receita") }, new()),
            CriarRegistro(mesAnterior, new() { Item("Venda", 500m, "Vendas", "Receita") }, new()),
        };

        var resultado = _sut.CalcularIndicadores(registros, mesReferencia: new DateOnly(mesPassado.Year, mesPassado.Month, 1));

        Assert.Equal(100m, resultado.VariacaoReceitaMesAnterior); // 1000 vs 500 = +100%
    }

    // ---- Ponto de Equilíbrio ----

    [Fact]
    public void CalcularIndicadores_PontoEquilibrio_CalculaValorMensalDiaUtilEDistancia()
    {
        var hoje = DataLocalHelper.Hoje();
        var reg = CriarRegistro(hoje,
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new()
            {
                Item("Insumos", 200m, "Insumos/Mercadoria", "CustoVariavel"),
                Item("Aluguel", 300m, "Aluguel", "CustoFixo"),
            });

        var resultado = _sut.CalcularIndicadores(new() { reg }).PontoEquilibrio;

        // MC = 1000 - 200 = 800 (80% da receita); PE = 300 / 0.8 = 375
        Assert.True(resultado.Disponivel);
        Assert.Equal(375m, resultado.ValorMensal);
        Assert.Equal(1000m, resultado.ReceitaAtual);
        Assert.Equal(625m, resultado.Distancia); // 1000 - 375
        Assert.NotNull(resultado.DiasUteisNoMes);
        Assert.Equal(Math.Round(375m / resultado.DiasUteisNoMes!.Value, 2), resultado.ValorPorDiaUtil);
    }

    [Fact]
    public void CalcularIndicadores_PontoEquilibrio_CustoVariavelMaiorQueReceita_Indisponivel()
    {
        var hoje = DataLocalHelper.Hoje();
        var reg = CriarRegistro(hoje,
            new() { Item("Venda", 100m, "Vendas", "Receita") },
            new() { Item("Insumos", 200m, "Insumos/Mercadoria", "CustoVariavel") });

        var resultado = _sut.CalcularIndicadores(new() { reg }).PontoEquilibrio;

        Assert.False(resultado.Disponivel);
        Assert.Null(resultado.ValorMensal);
        Assert.NotNull(resultado.MotivoIndisponivel);
    }

    [Fact]
    public void CalcularIndicadores_PontoEquilibrio_SemReceita_Indisponivel()
    {
        var hoje = DataLocalHelper.Hoje();
        var reg = CriarRegistro(hoje, new(), new() { Item("Aluguel", 300m, "Aluguel", "CustoFixo") });

        var resultado = _sut.CalcularIndicadores(new() { reg }).PontoEquilibrio;

        Assert.False(resultado.Disponivel);
        Assert.NotNull(resultado.MotivoIndisponivel);
    }

    // ---- Fôlego de Caixa ----

    [Fact]
    public void CalcularIndicadores_FolegoCaixa_CalculaMesesEFaixaConfortavel()
    {
        var hoje = DataLocalHelper.Hoje();
        var contaId = Guid.NewGuid();
        var registros = new List<RegistroDiario>();
        for (int i = 0; i < 3; i++)
        {
            var reg = CriarRegistro(hoje.AddMonths(-i),
                new() { Item("Venda", 2000m, "Vendas", "Receita") },
                new() { Item("Aluguel", 1000m, "Aluguel", "CustoFixo") },
                saldoFinal: 5000m - i * 100);
            reg.ContaBancariaId = contaId;
            registros.Add(reg);
        }

        var resultado = _sut.CalcularIndicadores(registros, saldoDisponivel: 5000m).FolegoCaixa;

        Assert.True(resultado.Disponivel);
        Assert.Equal(1000m, resultado.CustoFixoMedioMensal);
        Assert.Equal(5000m, resultado.SaldoDisponivel); // mesmo saldo consolidado passado pelo controller (1.5)
        Assert.Equal(5.0m, resultado.Meses);
        Assert.Equal("confortavel", resultado.Faixa);
    }

    [Fact]
    public void CalcularIndicadores_FolegoCaixa_SaldoBaixo_FaixaCritica()
    {
        var hoje = DataLocalHelper.Hoje();
        var contaId = Guid.NewGuid();
        var reg = CriarRegistro(hoje,
            new() { Item("Venda", 2000m, "Vendas", "Receita") },
            new() { Item("Aluguel", 1000m, "Aluguel", "CustoFixo") },
            saldoFinal: 500m);
        reg.ContaBancariaId = contaId;

        var resultado = _sut.CalcularIndicadores(new() { reg }, saldoDisponivel: 500m).FolegoCaixa;

        Assert.True(resultado.Disponivel);
        Assert.Equal(0.5m, resultado.Meses);
        Assert.Equal("critico", resultado.Faixa);
    }

    [Fact]
    public void CalcularIndicadores_FolegoCaixa_SemCustoFixo_Indisponivel()
    {
        var hoje = DataLocalHelper.Hoje();
        var reg = CriarRegistro(hoje, new() { Item("Venda", 2000m, "Vendas", "Receita") }, new());
        reg.ContaBancariaId = Guid.NewGuid();

        var resultado = _sut.CalcularIndicadores(new() { reg }).FolegoCaixa;

        Assert.False(resultado.Disponivel);
        Assert.NotNull(resultado.MotivoIndisponivel);
    }

    // ---- Custo Fixo Mensal (série de 6 meses) ----

    [Fact]
    public void CalcularIndicadores_CustoFixoMensal_RetornaSeisMesesComPercentual()
    {
        var hoje = DataLocalHelper.Hoje();
        var reg = CriarRegistro(hoje,
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new() { Item("Aluguel", 400m, "Aluguel", "CustoFixo") });

        var resultado = _sut.CalcularIndicadores(new() { reg }).CustoFixoMensal;

        Assert.Equal(6, resultado.Count);
        var mesAtual = resultado[^1];
        Assert.Equal(1000m, mesAtual.Receita);
        Assert.Equal(400m, mesAtual.CustoFixo);
        Assert.Equal(40.0m, mesAtual.Percentual);
    }

    // ---- Prazo Médio de Recebimento ----

    [Fact]
    public void CalcularIndicadores_PrazoRecebimento_PoucasAmostras_Indisponivel()
    {
        var hoje = DataLocalHelper.Hoje();
        var reg = CriarRegistro(hoje, new(), new());
        reg.ContasReceber = new()
        {
            new() { Descricao = "A", Valor = 100m, Pago = true, DataVencimento = hoje, DataBaixa = hoje },
        };

        var resultado = _sut.CalcularIndicadores(new() { reg }).PrazoRecebimento;

        Assert.False(resultado.Disponivel);
        Assert.Equal(1, resultado.QuantidadeAmostras);
    }

    [Fact]
    public void CalcularIndicadores_PrazoRecebimento_MaioriaMesmoDia_IndisponivelComMensagemExplicativa()
    {
        var hoje = DataLocalHelper.Hoje();
        var reg = CriarRegistro(hoje, new(), new());
        reg.ContasReceber = Enumerable.Range(0, 6)
            .Select(i => new ContaProvisionada { Descricao = $"C{i}", Valor = 100m, Pago = true, DataVencimento = hoje.AddDays(-i), DataBaixa = hoje.AddDays(-i) })
            .ToList();

        var resultado = _sut.CalcularIndicadores(new() { reg }).PrazoRecebimento;

        Assert.False(resultado.Disponivel);
        Assert.Contains("mesma data prevista", resultado.MotivoIndisponivel);
    }

    [Fact]
    public void CalcularIndicadores_PrazoRecebimento_ComAtrasoReal_CalculaMediaDias()
    {
        var hoje = DataLocalHelper.Hoje();
        var reg = CriarRegistro(hoje, new(), new());
        var atrasos = new[] { 5, 10, 3, 7, 15 };
        reg.ContasReceber = atrasos
            .Select((d, i) => new ContaProvisionada { Descricao = $"C{i}", Valor = 100m, Pago = true, DataVencimento = hoje.AddDays(-30), DataBaixa = hoje.AddDays(-30 + d) })
            .ToList();

        var resultado = _sut.CalcularIndicadores(new() { reg }).PrazoRecebimento;

        Assert.True(resultado.Disponivel);
        Assert.Equal(8.0m, resultado.MediaDias); // média de 5,10,3,7,15 = 8
        Assert.Equal(5, resultado.QuantidadeAmostras);
    }

    [Fact]
    public void CalcularIndicadores_ContaMesesComAtividadeCorretamente()
    {
        var hoje = DataLocalHelper.Hoje();
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(hoje, new() { Item("Venda", 500m, "Vendas", "Receita") }, new()),
            CriarRegistro(hoje.AddMonths(-1), new() { Item("Venda", 500m, "Vendas", "Receita") }, new()),
        };

        var resultado = _sut.CalcularIndicadores(registros, mesesEvolucao: 6);

        Assert.Equal(2, resultado.MesesComAtividade);
    }

    [Fact]
    public void CalcularIndicadores_MesesEvolucaoMenorQue13_VariacaoAnoAnteriorNula()
    {
        var hoje = DataLocalHelper.Hoje();
        var reg = CriarRegistro(hoje, new() { Item("Venda", 500m, "Vendas", "Receita") }, new());

        var resultado = _sut.CalcularIndicadores(new() { reg }, mesesEvolucao: 6);

        Assert.Null(resultado.VariacaoReceitaAnoAnterior);
    }

    [Fact]
    public void CalcularIndicadores_ComMesAnteriorComReceita_CalculaVariacaoMesAMes()
    {
        var hoje = DataLocalHelper.Hoje();
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(hoje, new() { Item("Venda", 1200m, "Vendas", "Receita") }, new()),
            CriarRegistro(hoje.AddMonths(-1), new() { Item("Venda", 1000m, "Vendas", "Receita") }, new()),
        };

        var resultado = _sut.CalcularIndicadores(registros, mesesEvolucao: 6);

        Assert.Equal(20.0m, resultado.VariacaoReceitaMesAnterior);
    }

    // Item 5: o mês atual pode estar em andamento (só uns dias lançados) — comparar com o mês
    // anterior INTEIRO sempre mostra uma queda artificial. A variação tem que recortar o mês
    // anterior nos mesmos dias já percorridos neste mês.
    [Fact]
    public void CalcularIndicadores_MesAtualEmAndamento_ComparaComOsMesmosDiasDoMesAnterior()
    {
        var hoje = DataLocalHelper.Hoje();
        var corteAnterior = hoje.AddMonths(-1); // mesmo dia-do-mês (ou o último, se o mês for mais curto)
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(hoje, new() { Item("Venda", 120m, "Vendas", "Receita") }, new()),
            // Dentro da janela de comparação (até o mesmo ponto do mês anterior) — conta.
            CriarRegistro(corteAnterior, new() { Item("Venda", 100m, "Vendas", "Receita") }, new()),
            // Depois da janela — a parte do mês anterior que ainda nem aconteceu neste mês. Nunca
            // pode contar, senão um mês anterior "cheio" sempre parece uma queda forte no meio do mês.
            CriarRegistro(corteAnterior.AddDays(1), new() { Item("Venda", 900m, "Vendas", "Receita") }, new()),
        };

        var resultado = _sut.CalcularIndicadores(registros, mesesEvolucao: 6);

        // 120 vs 100 (só o que já tinha acontecido até o mesmo ponto do mês anterior) = +20%, não a
        // queda de quase 90% que (120 vs 1000, mês anterior inteiro) daria.
        Assert.Equal(20.0m, resultado.VariacaoReceitaMesAnterior);
    }

    [Fact]
    public void CalcularIndicadores_MesAnteriorSemReceita_VariacaoMesAMesNula()
    {
        var hoje = DataLocalHelper.Hoje();
        var reg = CriarRegistro(hoje, new() { Item("Venda", 1200m, "Vendas", "Receita") }, new());

        var resultado = _sut.CalcularIndicadores(new() { reg }, mesesEvolucao: 6);

        Assert.Null(resultado.VariacaoReceitaMesAnterior);
    }

    [Fact]
    public void CalcularIndicadores_Com13MesesEMesmoMesAnoAnterior_CalculaVariacaoAnoAnterior()
    {
        var hoje = DataLocalHelper.Hoje();
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(hoje, new() { Item("Venda", 1500m, "Vendas", "Receita") }, new()),
            CriarRegistro(hoje.AddMonths(-12), new() { Item("Venda", 1000m, "Vendas", "Receita") }, new()),
        };

        var resultado = _sut.CalcularIndicadores(registros, mesesEvolucao: 13);

        Assert.Equal(50.0m, resultado.VariacaoReceitaAnoAnterior);
    }

    [Fact]
    public void CalcularIndicadores_CategoriaComGastoAtualMenorQueMedia_VariacaoPercentualNegativa()
    {
        var hoje = DataLocalHelper.Hoje();
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(hoje, new(), new() { Item("Aluguel", 10m, "Aluguel", "CustoFixo") }),
        };
        for (int i = 1; i <= 3; i++)
            registros.Add(CriarRegistro(hoje.AddMonths(-i), new(), new() { Item("Aluguel", 500m, "Aluguel", "CustoFixo") }));

        var resultado = _sut.CalcularIndicadores(registros);

        var categoria = resultado.RankingCategorias.Single(c => c.Nome == "Aluguel");
        Assert.Equal(500m, categoria.MediaMesesAnteriores);
        Assert.NotNull(categoria.VariacaoPercentual);
        Assert.True(categoria.VariacaoPercentual < 0);
    }

    // ---- Semáforos: Ebitda ----

    [Fact]
    public void CalcularPeriodo_EbitdaEntre5e15Porcento_SemaforoAmarelo()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new() { Item("Custo", 920m, "Aluguel", "CustoFixo") });
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg }, 0m);
        Assert.Equal("amarelo", resultado.Ebitda!.Semaforo);
    }

    [Fact]
    public void CalcularPeriodo_EbitdaAbaixoDe5Porcento_SemaforoVermelho()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new() { Item("Custo", 980m, "Aluguel", "CustoFixo") });
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg }, 0m);
        Assert.Equal("vermelho", resultado.Ebitda!.Semaforo);
    }

    // ---- Semáforos: PrimeCost ----

    [Fact]
    public void CalcularPeriodo_PrimeCostAbaixoDe60Porcento_SemaforoVerde()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new() { Item("Salarios", 500m, "Salários/Folha", "CustoFixo") });
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg }, 0m);
        Assert.Equal("verde", resultado.PrimeCost!.Semaforo);
    }

    [Fact]
    public void CalcularPeriodo_PrimeCostAcimaDe75Porcento_SemaforoVermelho()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new() { Item("Salarios", 800m, "Salários/Folha", "CustoFixo") });
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg }, 0m);
        Assert.Equal("vermelho", resultado.PrimeCost!.Semaforo);
    }

    // ---- Semáforos: Ponto de Equilíbrio ----

    [Fact]
    public void CalcularPeriodo_ReceitaAbaixoDoPontoDeEquilibrio_SemaforoAmarelo()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new() { Item("Custo", 900m, "Aluguel", "CustoFixo") });
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg }, 0m);
        Assert.Equal(900m, resultado.PontoDeEquilibrio!.Valor);
        Assert.Equal("amarelo", resultado.PontoDeEquilibrio.Semaforo);
    }

    [Fact]
    public void CalcularPeriodo_ReceitaMuitoAbaixoDoPontoDeEquilibrio_SemaforoVermelho()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new() { Item("Custo", 1200m, "Aluguel", "CustoFixo") });
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg }, 0m);
        Assert.Equal("vermelho", resultado.PontoDeEquilibrio!.Semaforo);
    }

    // Money Vision: "números que se contradizem" (item 1) — Ponto de Equilíbrio do Dashboard agora
    // vem do mesmo CalcularDre/CalcularPontoEquilibrio da tela de DRE/Indicadores. Sem margem de
    // contribuição pra cobrir despesas fixas, o valor fica indisponível (null) — nunca um "0,
    // verde" artificial, que dava a entender (errado) que a empresa já bateu o ponto de equilíbrio.
    [Fact]
    public void CalcularPeriodo_MargemContribuicaoZeroOuNegativa_PontoDeEquilibrioIndisponivel()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new()
            {
                Item("Custo Fixo", 100m, "Aluguel", "CustoFixo"),
                Item("Custo Variavel", 1000m, "Insumos/Mercadoria", "CustoVariavel"),
            });
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg }, 0m);
        Assert.Null(resultado.PontoDeEquilibrio);
    }

    // ---- Semáforos: Valuation ----

    [Fact]
    public void CalcularPeriodo_LucroAnteriorZero_ValuationSemaforoCinza()
    {
        var hoje = DataLocalHelper.Hoje();
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(hoje, new() { Item("Venda", 1000m, "Vendas", "Receita") }, new() { Item("Custo", 500m, "Aluguel", "CustoFixo") }),
            CriarRegistro(hoje.AddMonths(-1), new() { Item("Venda", 500m, "Vendas", "Receita") }, new() { Item("Custo", 500m, "Aluguel", "CustoFixo") }),
        };

        var resultado = _sut.CalcularPeriodo(registros, registros, 0m);

        Assert.Equal("cinza", resultado.Valuation!.Semaforo);
    }

    [Fact]
    public void CalcularPeriodo_LucroSubiuMaisDe5Porcento_ValuationSemaforoVerde()
    {
        var hoje = DataLocalHelper.Hoje();
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(hoje, new() { Item("Venda", 1200m, "Vendas", "Receita") }, new()),
            CriarRegistro(hoje.AddMonths(-1), new() { Item("Venda", 1000m, "Vendas", "Receita") }, new()),
        };

        var resultado = _sut.CalcularPeriodo(registros, registros, 0m);

        Assert.Equal("verde", resultado.Valuation!.Semaforo);
    }

    [Fact]
    public void CalcularPeriodo_LucroCaiuMaisDe5Porcento_ValuationSemaforoVermelho()
    {
        var hoje = DataLocalHelper.Hoje();
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(hoje, new() { Item("Venda", 800m, "Vendas", "Receita") }, new()),
            CriarRegistro(hoje.AddMonths(-1), new() { Item("Venda", 1000m, "Vendas", "Receita") }, new()),
        };

        var resultado = _sut.CalcularPeriodo(registros, registros, 0m);

        Assert.Equal("vermelho", resultado.Valuation!.Semaforo);
    }

    [Fact]
    public void CalcularPeriodo_LucroEstavel_ValuationSemaforoAmarelo()
    {
        var hoje = DataLocalHelper.Hoje();
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(hoje, new() { Item("Venda", 1020m, "Vendas", "Receita") }, new()),
            CriarRegistro(hoje.AddMonths(-1), new() { Item("Venda", 1000m, "Vendas", "Receita") }, new()),
        };

        var resultado = _sut.CalcularPeriodo(registros, registros, 0m);

        Assert.Equal("amarelo", resultado.Valuation!.Semaforo);
    }

    // ---- Semáforos: Runway ----

    [Fact]
    public void CalcularPeriodo_SemGastosNosUltimos3Meses_RunwaySemaforoCinza()
    {
        var hoje = DataLocalHelper.Hoje();
        var registro = CriarRegistro(hoje, new() { Item("Venda", 500m, "Vendas", "Receita") }, new(), saldoFinal: 1000m);

        var resultado = _sut.CalcularPeriodo(new() { registro }, new() { registro }, 1000m);

        Assert.Equal("cinza", resultado.Runway!.Semaforo);
    }

    [Fact]
    public void CalcularPeriodo_RunwayMaiorQue6Meses_SemaforoVerde()
    {
        var hoje = DataLocalHelper.Hoje();
        var registro = CriarRegistro(hoje, new(), new() { Item("Custo", 100m, "Aluguel", "CustoFixo") }, saldoFinal: 800m);

        var resultado = _sut.CalcularPeriodo(new() { registro }, new() { registro }, 800m);

        Assert.Equal(8.0m, resultado.Runway!.Meses);
        Assert.Equal("verde", resultado.Runway.Semaforo);
    }

    [Fact]
    public void CalcularPeriodo_RunwayEntre3e6Meses_SemaforoAmarelo()
    {
        var hoje = DataLocalHelper.Hoje();
        var registro = CriarRegistro(hoje, new(), new() { Item("Custo", 100m, "Aluguel", "CustoFixo") }, saldoFinal: 400m);

        var resultado = _sut.CalcularPeriodo(new() { registro }, new() { registro }, 400m);

        Assert.Equal(4.0m, resultado.Runway!.Meses);
        Assert.Equal("amarelo", resultado.Runway.Semaforo);
    }

    [Fact]
    public void CalcularPeriodo_RunwayMenorQue3Meses_SemaforoVermelho()
    {
        var hoje = DataLocalHelper.Hoje();
        var registro = CriarRegistro(hoje, new(), new() { Item("Custo", 100m, "Aluguel", "CustoFixo") }, saldoFinal: 100m);

        var resultado = _sut.CalcularPeriodo(new() { registro }, new() { registro }, 100m);

        Assert.Equal(1.0m, resultado.Runway!.Meses);
        Assert.Equal("vermelho", resultado.Runway.Semaforo);
    }

    // Bloco 2 da rodada "Visão de Futuro": Dashboard (Runway) e Indicadores (Fôlego de Caixa)
    // precisam SEMPRE bater pro mesmo saldo — antes, Runway usava 3 meses (soma bruta por
    // TipoCusto) e Fôlego de Caixa usava 6 meses só com atividade (DespesasFixas do DRE); com
    // custo fixo crescendo mês a mês, essas duas janelas davam médias (e portanto Runway)
    // diferentes pro mesmo cliente. Agora os dois usam exatamente o mesmo cálculo.
    [Fact]
    public void CalcularPeriodo_E_CalcularIndicadores_RunwayEFolegoDeCaixaBatemParaOMesmoSaldo()
    {
        var hoje = DataLocalHelper.Hoje();
        var registros = new List<RegistroDiario>();
        // 3 meses mais antigos com custo fixo baixo (200) e os 3 mais recentes com custo fixo
        // alto (1000) — média dos últimos 3 meses (fórmula antiga do Runway) seria 1000; média
        // dos 6 meses com atividade (fórmula do Fôlego de Caixa) é 600.
        for (int i = 5; i >= 3; i--)
            registros.Add(CriarRegistro(hoje.AddMonths(-i).AddDays(-1),
                new(), new() { Item("Custo", 200m, "Aluguel", "CustoFixo") }));
        for (int i = 2; i >= 0; i--)
            registros.Add(CriarRegistro(hoje.AddMonths(-i).AddDays(-1),
                new(), new() { Item("Custo", 1000m, "Aluguel", "CustoFixo") }));

        var periodo = _sut.CalcularPeriodo(registros, registros, 6000m);
        var indicadores = _sut.CalcularIndicadores(registros, saldoDisponivel: 6000m);

        Assert.True(indicadores.FolegoCaixa.Disponivel);
        Assert.Equal(600m, indicadores.FolegoCaixa.CustoFixoMedioMensal);
        Assert.Equal(600m, periodo.BurnRate);
        Assert.Equal(indicadores.FolegoCaixa.Meses, periodo.Runway!.Meses);
        Assert.Equal(10.0m, periodo.Runway.Meses);
    }

    // ---- Semáforos: Liquidez ----

    [Fact]
    public void CalcularPeriodo_IndiceLiquidezEntre1e15_SemaforoAmarelo()
    {
        var hoje = DataLocalHelper.Hoje();
        var amanha = hoje.AddDays(1);
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Data = hoje,
            Entradas = new(), Saidas = new(), SaldoFinal = 1200m,
            ContasReceber = new(),
            ContasPagar = new() { new() { Descricao = "Fornecedor", Valor = 1000m, DataVencimento = amanha, Pago = false } },
        };

        var resultado = _sut.CalcularPeriodo(new() { registro }, new() { registro }, 1200m);

        Assert.Equal(1.2m, resultado.Liquidez!.Indice);
        Assert.Equal("amarelo", resultado.Liquidez.Semaforo);
    }

    [Fact]
    public void CalcularPeriodo_IndiceLiquidezAbaixoDe1_SemaforoVermelho()
    {
        var hoje = DataLocalHelper.Hoje();
        var amanha = hoje.AddDays(1);
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Data = hoje,
            Entradas = new(), Saidas = new(), SaldoFinal = 300m,
            ContasReceber = new(),
            ContasPagar = new() { new() { Descricao = "Fornecedor", Valor = 1000m, DataVencimento = amanha, Pago = false } },
        };

        var resultado = _sut.CalcularPeriodo(new() { registro }, new() { registro }, 300m);

        Assert.Equal(0.3m, resultado.Liquidez!.Indice);
        Assert.Equal("vermelho", resultado.Liquidez.Semaforo);
    }

    // ---- DRE: margem nula sem receita ----

    [Fact]
    public void CalcularDre_SemReceita_MargemNula()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1), new(), new() { Item("Custo", 300m, "Aluguel", "CustoFixo") });

        var resultado = _sut.CalcularDre(new() { reg });

        Assert.Equal(0m, resultado.ReceitaBruta);
        Assert.Null(resultado.Margem);
    }

    // ---- DRE: análise vertical (waterfall) ----

    private static readonly List<Categoria> _planoDeContasTeste = new()
    {
        Cat("Vendas", "Receita"),
        Cat("Simples/DAS", "CustoFixo", grupo: "Impostos"),
        Cat("Insumos/Mercadoria", "CustoVariavel", grupo: "Custos Diretos"),
        Cat("Aluguel", "CustoFixo", grupo: "Despesas Administrativas"),
        Cat("Equipamentos", "DespesaNaoOperacional", grupo: "Investimentos"),
    };

    // Rodada de ajustes Money Vision, item 4 (DRE mais enxuta): Despesas Não Operacionais e Não
    // Classificado deixaram de ter linha própria — ambas somam dentro de Despesas Fixas (mesmo
    // bucket que a árvore hierárquica Bloco→Grupo→Categoria já usava).
    [Fact]
    public void CalcularDre_AnaliseVertical_ClassificaCadaLinhaCorretamente()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new()
            {
                Item("DAS", 60m, "Simples/DAS", "CustoFixo"),
                Item("Insumos", 280m, "Insumos/Mercadoria", "CustoVariavel"),
                Item("Aluguel", 400m, "Aluguel", "CustoFixo"),
                Item("Notebook", 30m, "Equipamentos", "DespesaNaoOperacional"),
                Item("Sem categoria", 10m, null, null),
            });

        var dre = _sut.CalcularDre(new() { reg }, _planoDeContasTeste);

        Assert.Equal(1000m, dre.ReceitaBruta);
        Assert.Equal(100.0m, dre.ReceitaBrutaPercentual);

        Assert.Equal(60m, dre.Deducoes.Total);
        Assert.Equal(6.0m, dre.Deducoes.Percentual);
        Assert.Equal(940m, dre.ReceitaLiquida);
        Assert.Equal(94.0m, dre.ReceitaLiquidaPercentual);

        Assert.Equal(280m, dre.CustosVariaveis.Total);
        Assert.Equal(660m, dre.MargemContribuicao);
        Assert.Equal(66.0m, dre.MargemContribuicaoPercentual);

        // Aluguel (400) + Notebook/DespesaNaoOperacional (30) + Sem categoria (10) — as 3 caem
        // juntas em Despesas Fixas, nenhuma com linha própria.
        Assert.Equal(440m, dre.DespesasFixas.Total);
        Assert.Equal(220m, dre.ResultadoOperacional);
        Assert.Equal(22.0m, dre.ResultadoOperacionalPercentual);
        Assert.Equal(1, dre.QuantidadeNaoClassificados); // só a saída "Sem categoria"

        Assert.Equal(220m, dre.ResultadoLiquido);
        Assert.Equal(22.0m, dre.ResultadoLiquidoPercentual);

        // Reconciliação: a soma das linhas visíveis bate com o total de despesas de sempre, e o
        // resultado continua batendo com a soma das linhas (critério de aceite do item 4).
        Assert.Equal(dre.TotalDespesas, dre.Deducoes.Total + dre.CustosVariaveis.Total + dre.DespesasFixas.Total);
        Assert.Equal(dre.Resultado, dre.ResultadoLiquido);
    }

    // Money Vision: "os subtotais não fecham com as linhas mostradas" (item 2) — todo subtotal do
    // DRE tem que ser exatamente a soma das linhas imediatamente acima dele na tela (Receitas
    // Operacionais → Deduções → Custos Operacionais → Margem de Contribuição → Despesas
    // Operacionais → Resultado Operacional → Atividades de Investimento/Financiamento →
    // Retiradas dos Sócios → Resultado Líquido). Cobre vários meses sintéticos diferentes —
    // inclusive os valores exatos do relato ("15.895,54 receita, 9.021,02 custos operacionais")
    // pra provar que o "furo" era a linha de Deduções não contada na conta manual, não um bug.
    [Theory]
    [InlineData(15895.54, 2255.94, 9021.02, 4474.08, 0, 0, 0)] // cenário exato do relato
    [InlineData(1000, 0, 0, 0, 0, 0, 0)] // sem nenhuma dedução/custo/despesa
    [InlineData(5000, 100, 2000, 800, 300, -150, 200)] // com atividades de investimento/financiamento e retiradas
    [InlineData(800, 50, 900, 0, 0, 0, 0)] // margem de contribuição negativa
    public void CalcularDre_SubtotaisSempreFecham_ComVariosMesesSinteticos(
        decimal receita, decimal deducao, decimal custoVariavel, decimal despesaFixa,
        decimal atividadeInvestimento, decimal atividadeFinanciamento, decimal retiradaSocios)
    {
        var entradas = new List<ItemFinanceiro> { Item("Receita", receita, "Vendas", "Receita") };
        if (atividadeInvestimento > 0) entradas.Add(Item("Captação", atividadeInvestimento, "Aporte", "Investimento"));
        var saidas = new List<ItemFinanceiro>();
        if (deducao > 0) saidas.Add(Item("Imposto", deducao, "Impostos", "CustoFixo"));
        if (custoVariavel > 0) saidas.Add(Item("Insumos", custoVariavel, "Insumos/Mercadoria", "CustoVariavel"));
        if (despesaFixa > 0) saidas.Add(Item("Aluguel", despesaFixa, "Aluguel", "CustoFixo"));
        if (atividadeInvestimento < 0) saidas.Add(Item("Aquisição", -atividadeInvestimento, "Equipamento", "Investimento"));
        if (atividadeFinanciamento > 0) entradas.Add(Item("Empréstimo", atividadeFinanciamento, "Empréstimo", "Financiamento"));
        if (atividadeFinanciamento < 0) saidas.Add(Item("Amortização", -atividadeFinanciamento, "Amortização", "Financiamento"));
        if (retiradaSocios > 0) saidas.Add(Item("Pró-labore", retiradaSocios, "Retirada", "RetiradaSocios"));

        var reg = CriarRegistro(new DateOnly(2026, 10, 1), entradas, saidas);
        var dre = _sut.CalcularDre(new() { reg });

        Assert.Equal(dre.MargemContribuicao, dre.ReceitaBruta - dre.Deducoes.Total - dre.CustosVariaveis.Total);
        Assert.Equal(dre.ResultadoOperacional, dre.MargemContribuicao - dre.DespesasFixas.Total);
        Assert.Equal(dre.ResultadoLiquido,
            dre.ResultadoOperacional + dre.AtividadesInvestimento.Total + dre.AtividadesFinanciamento.Total - dre.RetiradasSocios.Total);
    }

    // Item 3 (preservado na rodada de ajustes): "Não Classificados" precisa ser o valor LÍQUIDO
    // (saídas sem categoria − entradas sem categoria), não só a saída — senão a entrada sem
    // categoria fica contada a mais (uma vez cheia na Receita Bruta, sem nenhuma linha descontando
    // ela) e a soma das linhas visíveis não bate com o Resultado Líquido. Agora esse líquido entra
    // dentro de Despesas Fixas, não numa linha própria.
    [Fact]
    public void CalcularDre_EntradaSemCategoria_DescontaDaReceitaBrutaEEntraNetaDentroDeDespesasFixas()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new()
            {
                Item("Venda", 900m, "Vendas", "Receita"),
                Item("Pix sem categoria", 100m, null, null),
            },
            new()
            {
                Item("Aluguel", 400m, "Aluguel", "CustoFixo"),
                Item("Saída sem categoria", 50m, null, null),
            });

        var dre = _sut.CalcularDre(new() { reg }, _planoDeContasTeste);

        // Receita Bruta não inclui o Pix sem categoria (900, não 1000).
        Assert.Equal(900m, dre.ReceitaBruta);
        // Despesas Fixas = 400 (Aluguel) + [50 (saída sem cat) − 100 (entrada sem cat)] = 350.
        Assert.Equal(350m, dre.DespesasFixas.Total);
        Assert.Equal(2, dre.QuantidadeNaoClassificados); // 1 entrada + 1 saída sem categoria

        Assert.Equal(550m, dre.ResultadoOperacional); // 900 - 350
        Assert.Equal(550m, dre.ResultadoLiquido);

        // Reconciliação: somando as linhas visíveis da cascata chega-se exatamente no Resultado Líquido.
        Assert.Equal(dre.ResultadoLiquido,
            dre.ReceitaBruta - dre.Deducoes.Total - dre.CustosVariaveis.Total - dre.DespesasFixas.Total
            + dre.AtividadesInvestimento.Total + dre.AtividadesFinanciamento.Total - dre.RetiradasSocios.Total);
    }

    [Fact]
    public void CalcularDre_CategoriaNaoCadastradaNoPlanoDeContas_VaiParaDespesasFixas()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 500m, "Vendas", "Receita") },
            new() { Item("Gasto estranho", 50m, "Categoria Removida", "CustoFixo") });

        var dre = _sut.CalcularDre(new() { reg }, _planoDeContasTeste);

        Assert.Equal(50m, dre.DespesasFixas.Total);
        Assert.Equal("Categoria Removida", Assert.Single(dre.DespesasFixas.Categorias).Nome);
        Assert.Equal(0, dre.QuantidadeNaoClassificados); // tem categoria, só não está cadastrada
    }

    [Fact]
    public void CalcularDre_IgnoraTransferencias_NaoEntramNemComoNaoClassificado()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new()
            {
                Item("Venda", 1000m, "Vendas", "Receita"),
                Item("Resgate de investimento", 5000m, "Transferência", "Transferencia"),
            },
            new()
            {
                Item("Aluguel", 400m, "Aluguel", "CustoFixo"),
                Item("Aporte para investimento", 2000m, "Transferência", "Transferencia"),
            });

        var dre = _sut.CalcularDre(new() { reg }, _planoDeContasTeste);

        Assert.Equal(1000m, dre.ReceitaBruta); // resgate (transferência) não entra na receita
        Assert.Equal(400m, dre.TotalDespesas); // só o aluguel — transferência fica de fora
        Assert.Equal(0, dre.QuantidadeNaoClassificados); // não vira "Não Classificado": é excluída, não desconhecida
        Assert.Equal(600m, dre.Resultado);
    }

    // Money Vision — Bloco 3: linha de ajuste de saldo (ex.: descrição "saldo") fica fora do DRE
    // mesmo tendo uma Categoria "normal" ("Devoluções") — é o TipoCusto "AjusteSaldo" que decide,
    // não a categoria.
    [Fact]
    public void CalcularDre_AjusteDeSaldo_NaoEntraNoDreMesmoComCategoriaNormal()
    {
        var reg = CriarRegistro(new DateOnly(2026, 9, 30),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new()
            {
                Item("Aluguel", 400m, "Aluguel", "CustoFixo"),
                Item("saldo", 3895.76m, "Devoluções", "AjusteSaldo"),
            });

        var dre = _sut.CalcularDre(new() { reg }, _planoDeContasTeste);

        Assert.Equal(400m, dre.TotalDespesas); // só o aluguel — o ajuste de saldo não conta
        Assert.Equal(0, dre.QuantidadeNaoClassificados);
        Assert.Equal(0m, dre.Deducoes.Total); // "Devoluções" normalmente cairia em Deduções, mas não aqui
        Assert.Equal(600m, dre.Resultado);
    }

    // Rodada de ajustes Money Vision, item 4: Receita Financeira deixou de ser uma linha própria
    // depois do Resultado Operacional — rendimento de investimento agora soma direto na Receita
    // Operacional (metodologia simplificada: dinheiro que entrou é receita), por isso também muda
    // o Resultado Operacional/Margem, não só o Resultado Líquido final.
    [Fact]
    public void CalcularDre_ComRendimento_SomaNaReceitaOperacionalComoOutrasReceitas()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new()
            {
                Item("Venda", 1000m, "Vendas", "Receita"),
                Item("Rendimento CDI", 100m, "Rendimento", "Rendimento"),
            },
            new()
            {
                Item("Aluguel", 400m, "Aluguel", "CustoFixo"),
                Item("Rendimento negativo", 20m, "Rendimento", "Rendimento"),
            });

        var dre = _sut.CalcularDre(new() { reg }, _planoDeContasTeste);

        // 1000 (vendas) + [100 (rendimento) - 20 (rendimento negativo)] = 1080.
        Assert.Equal(1080m, dre.ReceitaBruta);
        Assert.Equal(680m, dre.ResultadoOperacional); // 1080 - 400, rendimento já dentro da receita
        Assert.Equal(680m, dre.ResultadoLiquido);
    }

    [Fact]
    public void CalcularDre_SemPlanoDeContas_UsaTipoCustoDoLancamentoEMapaFixoParaImpostos()
    {
        // Sem passar `categorias` (compat com chamadas antigas/testes): cai no TipoCusto gravado
        // no lançamento e no mapa fixo histórico para reconhecer o grupo "Impostos".
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 200m, "Vendas", "Receita") },
            new()
            {
                Item("DAS", 20m, "Simples/DAS", "CustoFixo"),
                Item("Aluguel", 30m, "Aluguel", "CustoFixo"),
            });

        var dre = _sut.CalcularDre(new() { reg });

        Assert.Equal(20m, dre.Deducoes.Total);
        Assert.Equal(30m, dre.DespesasFixas.Total);
    }

    // ---- DRE: hierarquia Bloco → Grupo → Categoria ----

    private static Categoria CatComBloco(string nome, string tipo, string grupoNome, string bloco, int ordem = 0) =>
        new() { Id = Guid.NewGuid(), Nome = nome, Tipo = tipo, GrupoId = Guid.NewGuid(), Grupo = new Grupo { Nome = grupoNome, Bloco = bloco }, Ordem = ordem, Ativa = true };

    private static readonly List<Categoria> _planoComBlocos = new()
    {
        CatComBloco("Vendas", "Receita", "Vendas e Serviços", Blocos.ReceitasOperacionais),
        CatComBloco("Simples/DAS", "CustoFixo", "Pagamento de Impostos", Blocos.DeducoesDaReceita),
        CatComBloco("Insumos/Mercadoria", "CustoVariavel", "Custos Diretos", Blocos.CustosOperacionais),
        CatComBloco("Aluguel", "CustoFixo", "Despesas com Ocupação", Blocos.DespesasOperacionais),
        CatComBloco("Publicidade", "CustoFixo", "Despesas com Marketing", Blocos.DespesasOperacionais),
        CatComBloco("Equipamentos", "Investimento", "Imobilizado", Blocos.AtividadesDeInvestimento),
        CatComBloco("Empréstimo Banco X", "Financiamento", "Empréstimos/Financiamentos", Blocos.AtividadesDeFinanciamento),
        CatComBloco("Pró-labore", "RetiradaSocios", "Pró-Labore / Retiradas", Blocos.RetiradasSocios),
    };

    [Fact]
    public void CalcularDre_AtividadeDeInvestimento_CaptacaoPositivaEAquisicaoNegativa()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new() { Item("Notebook", 300m, "Equipamentos", "Investimento") });

        var dre = _sut.CalcularDre(new() { reg }, _planoComBlocos);

        // Só saída de investimento nesse registro: net deve ser negativo (aquisição), não positivo.
        Assert.Equal(-300m, dre.AtividadesInvestimento.Total);
        Assert.Equal(700m, dre.ResultadoLiquido); // 1000 - 300, nada mais no exemplo
    }

    [Fact]
    public void CalcularDre_AtividadeDeInvestimento_NetaEntradaESaidaComSinalCorreto()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new()
            {
                Item("Venda", 1000m, "Vendas", "Receita"),
                Item("Aporte recebido", 500m, "Equipamentos", "Investimento"),
            },
            new() { Item("Compra equipamento", 200m, "Equipamentos", "Investimento") });

        var dre = _sut.CalcularDre(new() { reg }, _planoComBlocos);

        Assert.Equal(300m, dre.AtividadesInvestimento.Total); // 500 (entrada) - 200 (saída) = net positivo
    }

    [Fact]
    public void CalcularDre_AtividadeDeFinanciamento_AmortizacaoReduzResultadoLiquido()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new() { Item("Parcela empréstimo", 150m, "Empréstimo Banco X", "Financiamento") });

        var dre = _sut.CalcularDre(new() { reg }, _planoComBlocos);

        Assert.Equal(-150m, dre.AtividadesFinanciamento.Total);
        Assert.Equal(850m, dre.ResultadoLiquido);
    }

    [Fact]
    public void CalcularDre_InvestimentoEFinanciamento_NaoEntramNoResultadoOperacionalNemNaMargem()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new()
            {
                Item("Aluguel", 200m, "Aluguel", "CustoFixo"),
                Item("Compra equipamento", 300m, "Equipamentos", "Investimento"),
                Item("Parcela empréstimo", 100m, "Empréstimo Banco X", "Financiamento"),
            });

        var dre = _sut.CalcularDre(new() { reg }, _planoComBlocos);

        Assert.Equal(800m, dre.ResultadoOperacional); // 1000 - 200, sem investimento/financiamento
        Assert.Equal(400m, dre.ResultadoLiquido); // 800 - 300 - 100
    }

    // Correção de integridade (Parte 1, item 6): Pró-labore/retiradas dos sócios é a ÚLTIMA dedução
    // antes do Resultado Líquido — depois de Investimento e Financiamento, nunca despesa fixa.
    [Fact]
    public void CalcularDre_RetiradaDeSocios_UltimaDeducaoAntesDoResultadoLiquido()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new()
            {
                Item("Aluguel", 200m, "Aluguel", "CustoFixo"),
                Item("Compra equipamento", 300m, "Equipamentos", "Investimento"),
                Item("Parcela empréstimo", 100m, "Empréstimo Banco X", "Financiamento"),
                Item("Retirada sócio A", 150m, "Pró-labore", "RetiradaSocios"),
            });

        var dre = _sut.CalcularDre(new() { reg }, _planoComBlocos);

        Assert.Equal(150m, dre.RetiradasSocios.Total); // linha vertical: magnitude positiva, como DespesasFixas
        Assert.Equal(800m, dre.ResultadoOperacional); // 1000 - 200, pró-labore não é despesa operacional
        Assert.Equal(250m, dre.ResultadoLiquido); // 800 - 300 - 100 - 150

        var blocoRetiradas = dre.Blocos.Single(b => b.Bloco == Blocos.RetiradasSocios);
        Assert.Equal(-150m, blocoRetiradas.Total); // árvore: sinal de saída — mesmo valor usado no resultado
        Assert.Equal(Blocos.RetiradasSocios, Blocos.Ordem[^1]); // sempre a última posição de exibição
    }

    [Fact]
    public void CalcularDre_Blocos_MontaArvoreHierarquicaComTotaisETipo()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new()
            {
                Item("DAS", 60m, "Simples/DAS", "CustoFixo"),
                Item("Insumos", 100m, "Insumos/Mercadoria", "CustoVariavel"),
                Item("Aluguel", 200m, "Aluguel", "CustoFixo"),
                Item("Ads", 50m, "Publicidade", "CustoFixo"),
                Item("Notebook", 30m, "Equipamentos", "Investimento"),
                Item("Parcela", 40m, "Empréstimo Banco X", "Financiamento"),
            });

        var dre = _sut.CalcularDre(new() { reg }, _planoComBlocos);

        Assert.Equal(7, dre.Blocos.Count); // os 7 blocos sempre aparecem, mesmo os sem lançamento no período

        var receitas = dre.Blocos.Single(b => b.Bloco == Blocos.ReceitasOperacionais);
        Assert.Equal(1000m, receitas.Total);
        Assert.Equal(100.0m, receitas.Percentual);
        var grupoVendas = Assert.Single(receitas.Grupos);
        Assert.Equal("Vendas e Serviços", grupoVendas.Nome);
        Assert.Equal("Vendas", Assert.Single(grupoVendas.Categorias).Nome);

        var deducoes = dre.Blocos.Single(b => b.Bloco == Blocos.DeducoesDaReceita);
        Assert.Equal(-60m, deducoes.Total); // saída sempre com sinal negativo na árvore

        var despesas = dre.Blocos.Single(b => b.Bloco == Blocos.DespesasOperacionais);
        Assert.Equal(-250m, despesas.Total); // Aluguel (200) + Publicidade (50)
        Assert.Equal(2, despesas.Grupos.Count); // Ocupação e Marketing, cada um seu grupo

        var investimento = dre.Blocos.Single(b => b.Bloco == Blocos.AtividadesDeInvestimento);
        Assert.Equal(-30m, investimento.Total);

        var financiamento = dre.Blocos.Single(b => b.Bloco == Blocos.AtividadesDeFinanciamento);
        Assert.Equal(-40m, financiamento.Total);
    }

    // Rodada de ajustes Money Vision, item 4: na árvore hierárquica, rendimento de investimento
    // também passa a entrar em Receitas Operacionais (grupo "Outras receitas"), não mais num grupo
    // próprio dentro de Atividades de Investimento.
    [Fact]
    public void CalcularDre_Blocos_Rendimento_EntraEmReceitasOperacionaisComoOutrasReceitas()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new()
            {
                Item("Venda", 1000m, "Vendas", "Receita"),
                Item("Rendimento CDI", 100m, "Rendimento", "Rendimento"),
            },
            new() { Item("Rendimento negativo", 20m, "Rendimento", "Rendimento") });

        var dre = _sut.CalcularDre(new() { reg }, _planoComBlocos);

        var receitas = dre.Blocos.Single(b => b.Bloco == Blocos.ReceitasOperacionais);
        Assert.Equal(1080m, receitas.Total); // 1000 (vendas) + 80 (rendimento líquido)
        var grupoOutrasReceitas = Assert.Single(receitas.Grupos, g => g.Nome == "Outras receitas");
        Assert.Equal(80m, grupoOutrasReceitas.Total);

        var investimento = dre.Blocos.Single(b => b.Bloco == Blocos.AtividadesDeInvestimento);
        Assert.Equal(0m, investimento.Total); // rendimento não aparece mais aqui
    }

    [Fact]
    public void CalcularDre_Blocos_LancamentoSemCategoria_VaiParaNaoClassificadoDentroDoBloco()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 500m, "Vendas", "Receita") },
            new() { Item("Gasto sem categoria", 40m, null, null) });

        var dre = _sut.CalcularDre(new() { reg }, _planoComBlocos);

        var despesas = dre.Blocos.Single(b => b.Bloco == Blocos.DespesasOperacionais);
        var grupoSemClasse = Assert.Single(despesas.Grupos, g => g.Nome == "Não Classificado");
        Assert.Equal(-40m, grupoSemClasse.Total);
    }

    // Item A3 do bundle D6JWEAJa: a entrada sem categoria era lançada em cheio dentro de Receitas
    // Operacionais (inflando a receita) e o grupo "Não Classificado" de Despesas Operacionais só
    // mostrava a saída bruta — as duas pontas nunca se encontravam na visão por blocos, mesmo a
    // análise vertical já estando líquida (hoje, dentro de DespesasFixas). Agora a entrada sem
    // categoria cai no MESMO grupo "Não Classificado" de Despesas Operacionais, como crédito, e
    // Receitas Operacionais só soma entrada já classificada.
    [Fact]
    public void CalcularDre_Blocos_EntradaSemCategoria_NetaNoMesmoGrupoDeDespesasOperacionais()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 500m, "Vendas", "Receita"), Item("Entrada sem categoria", 100m, null, null) },
            new() { Item("Gasto sem categoria", 150m, null, null) });

        var dre = _sut.CalcularDre(new() { reg }, _planoComBlocos);

        var receitas = dre.Blocos.Single(b => b.Bloco == Blocos.ReceitasOperacionais);
        Assert.Equal(500m, receitas.Total); // não inclui a entrada sem categoria
        Assert.DoesNotContain(receitas.Grupos, g => g.Nome == "Não Classificado");

        var despesas = dre.Blocos.Single(b => b.Bloco == Blocos.DespesasOperacionais);
        var grupoSemClasse = Assert.Single(despesas.Grupos, g => g.Nome == "Não Classificado");
        Assert.Equal(-50m, grupoSemClasse.Total); // -150 (saída) + 100 (entrada) = -50 líquido

        Assert.Equal(500m, dre.ReceitaBruta);
    }

    // ---- CalcularPontoEquilibrio (promovido a público — painel de KPIs da tela de DRE) ----

    [Fact]
    public void CalcularPontoEquilibrio_ComMargemPositiva_CalculaValorEDistancia()
    {
        // Receita 1000, margem de contribuição 700 (70%), despesas fixas 300 → equilíbrio ≈ 428,57.
        var resultado = _sut.CalcularPontoEquilibrio(1000m, 700m, 300m, new DateOnly(2026, 6, 15));

        Assert.True(resultado.Disponivel);
        Assert.Equal(428.57m, resultado.ValorMensal);
        Assert.Equal(1000m, resultado.ReceitaAtual);
        Assert.Equal(571.43m, resultado.Distancia);
    }

    [Fact]
    public void CalcularPontoEquilibrio_SemReceita_Indisponivel()
    {
        var resultado = _sut.CalcularPontoEquilibrio(0m, 0m, 300m, new DateOnly(2026, 6, 15));

        Assert.False(resultado.Disponivel);
        Assert.NotNull(resultado.MotivoIndisponivel);
    }

    [Fact]
    public void CalcularPontoEquilibrio_MargemNaoPositiva_Indisponivel()
    {
        var resultado = _sut.CalcularPontoEquilibrio(1000m, 0m, 300m, new DateOnly(2026, 6, 15));

        Assert.False(resultado.Disponivel);
        Assert.NotNull(resultado.MotivoIndisponivel);
    }

    // ---- CalcularResultadoLiquidoMensal (sparkline do painel de KPIs da tela de DRE) ----

    [Fact]
    public void CalcularResultadoLiquidoMensal_RetornaSeisMesesAncoradosNoPeriodoSelecionado()
    {
        // Ancorado em agosto/2026 (não em "hoje") — a janela deve ir de março a agosto/2026.
        var reg = CriarRegistro(new DateOnly(2026, 8, 10),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new() { Item("Aluguel", 400m, "Aluguel", "CustoFixo") });

        var resultado = _sut.CalcularResultadoLiquidoMensal(new() { reg }, new DateOnly(2026, 8, 31), null);

        Assert.Equal(6, resultado.Count);
        Assert.Equal("2026-03", resultado[0].Mes);
        Assert.Equal("2026-08", resultado[^1].Mes);
        Assert.True(resultado[^1].TemDados);
        Assert.Equal(600m, resultado[^1].ResultadoLiquido);
        Assert.False(resultado[0].TemDados);
    }

    [Fact]
    public void CalcularResultadoLiquidoMensal_MesSemAtividade_TemDadosFalso()
    {
        var resultado = _sut.CalcularResultadoLiquidoMensal(new(), new DateOnly(2026, 8, 31), null);

        Assert.All(resultado, m => Assert.False(m.TemDados));
    }

    // ── Bloco 5 (Money Vision): "Para onde foi meu dinheiro" ───────────────────────────────────

    [Fact]
    public void CalcularParaOndeFoiMeuDinheiro_SeparaEntrouGastouGuardouEFicou()
    {
        var contaCorrenteId = Guid.NewGuid();
        var contaInvestimentoId = Guid.NewGuid();

        var regCorrente = CriarRegistro(new DateOnly(2026, 10, 5),
            new() { Item("Venda", 10000m, "Vendas", "Receita") },
            new()
            {
                Item("Aluguel", 2000m, "Aluguel", "CustoFixo"),
                Item("Aplicação RDB", 1300m, "Transferência", "Transferencia"), // sai da corrente pro investimento
            },
            contaBancariaId: contaCorrenteId);

        var regInvestimento = CriarRegistro(new DateOnly(2026, 10, 5),
            new() { Item("Aplicação RDB", 1300m, "Transferência", "Transferencia") }, // entra no investimento
            new(),
            contaBancariaId: contaInvestimentoId);

        var resultado = _sut.CalcularParaOndeFoiMeuDinheiro(
            new() { regCorrente, regInvestimento }, new HashSet<Guid> { contaInvestimentoId });

        Assert.Equal(10000m, resultado.Entrou); // transferência nunca conta como "entrou"
        Assert.Equal(2000m, resultado.Gastou); // idem "gastou"
        Assert.Equal(1300m, resultado.Guardou); // só a perna que caiu na conta de investimento
        Assert.Equal(6700m, resultado.Ficou); // 10000 - 2000 - 1300
        // Money Vision: taxa de poupança agora é sobre o resultado (entrou-gastou), nunca sobre o
        // aporte líquido em investimento (Guardou) — ver CalcularParaOndeFoiMeuDinheiro_ResgateNuncaDeixaATaxaNegativaComLucro.
        Assert.Equal(80.0m, resultado.TaxaPoupancaPercentual); // (10000-2000) / 10000
    }

    // Money Vision: "taxa de poupança negativa com lucro positivo" (item 3) — um resgate grande
    // da reserva (maior até que o lucro do mês) nunca pode deixar a taxa de poupança negativa,
    // porque ela não depende mais de Guardou.
    [Fact]
    public void CalcularParaOndeFoiMeuDinheiro_ResgateNuncaDeixaATaxaNegativaComLucro()
    {
        var contaInvestimentoId = Guid.NewGuid();
        var regCorrente = CriarRegistro(new DateOnly(2026, 10, 5),
            new() { Item("Venda", 15895.54m, "Vendas", "Receita") },
            new() { Item("Despesas", 13495.10m, "Geral", "CustoVariavel") },
            contaBancariaId: Guid.NewGuid());
        var regResgate = CriarRegistro(new DateOnly(2026, 10, 5),
            new(),
            new() { Item("Resgate", 1325.82m, "Transferência", "Transferencia") },
            contaBancariaId: contaInvestimentoId);

        var resultado = _sut.CalcularParaOndeFoiMeuDinheiro(
            new() { regCorrente, regResgate }, new HashSet<Guid> { contaInvestimentoId });

        Assert.Equal(-1325.82m, resultado.Guardou); // resgate aparece aqui, não na taxa
        Assert.Equal(15.1m, resultado.TaxaPoupancaPercentual); // (15895.54-13495.10)/15895.54, nunca negativa
    }

    [Fact]
    public void CalcularParaOndeFoiMeuDinheiro_ResgateReduzOGuardado()
    {
        var contaInvestimentoId = Guid.NewGuid();
        var reg = CriarRegistro(new DateOnly(2026, 10, 5),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new() { Item("Resgate", 300m, "Transferência", "Transferencia") },
            contaBancariaId: contaInvestimentoId);

        var resultado = _sut.CalcularParaOndeFoiMeuDinheiro(new() { reg }, new HashSet<Guid> { contaInvestimentoId });

        Assert.Equal(-300m, resultado.Guardou); // resgate é "guardado" negativo
    }

    [Fact]
    public void CalcularParaOndeFoiMeuDinheiro_SemReceita_TaxaDePoupancaFicaNull()
    {
        var resultado = _sut.CalcularParaOndeFoiMeuDinheiro(new(), new HashSet<Guid>());

        Assert.Null(resultado.TaxaPoupancaPercentual);
    }

    // Redesign visual (Money Vision): top 3 categorias de gasto, pro fluxo "Entrou → Gastou (top
    // 3) → Guardou → Ficou" — só apresentação, nunca muda o valor de Gastou.
    [Fact]
    public void CalcularParaOndeFoiMeuDinheiro_RetornaAs3MaioresCategoriasDeGasto()
    {
        var reg = CriarRegistro(new DateOnly(2026, 10, 5),
            new() { Item("Venda", 10000m, "Vendas", "Receita") },
            new()
            {
                Item("Aluguel", 2000m, "Aluguel", "CustoFixo"),
                Item("Folha", 3000m, "Folha de pagamento", "CustoFixo"),
                Item("Marketing", 500m, "Marketing", "CustoVariavel"),
                Item("Software", 100m, "Software", "CustoVariavel"),
            });

        var resultado = _sut.CalcularParaOndeFoiMeuDinheiro(new() { reg }, new HashSet<Guid>());

        Assert.Equal(3, resultado.TopCategoriasGasto.Count);
        Assert.Equal("Folha de pagamento", resultado.TopCategoriasGasto[0].Nome);
        Assert.Equal(3000m, resultado.TopCategoriasGasto[0].Total);
        Assert.Equal("Aluguel", resultado.TopCategoriasGasto[1].Nome);
        Assert.Equal("Marketing", resultado.TopCategoriasGasto[2].Nome);
        Assert.DoesNotContain(resultado.TopCategoriasGasto, c => c.Nome == "Software"); // 4º maior, fora do top 3
    }
}
