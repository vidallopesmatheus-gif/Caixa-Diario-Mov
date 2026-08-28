using CaixaDiario.API.Models;
using CaixaDiario.API.Services;

namespace CaixaDiario.Tests.Services;

public class MetricasServiceTests
{
    private readonly MetricasService _sut = new();

    private static RegistroDiario CriarRegistro(DateOnly data, List<ItemFinanceiro> entradas, List<ItemFinanceiro> saidas, decimal saldoFinal = 0) =>
        new()
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Data = data, Entradas = entradas,
            Saidas = saidas.Select(s => new ItemFinanceiroSaida { Descricao = s.Descricao, Valor = s.Valor, Categoria = s.Categoria ?? "", TipoCusto = s.TipoCusto }).ToList(),
            SaldoFinal = saldoFinal
        };

    private static ItemFinanceiro Item(string desc, decimal valor, string? categoria = null, string? tipoCusto = null) =>
        new() { Descricao = desc, Valor = valor, Categoria = categoria, TipoCusto = tipoCusto };

    private static Categoria Cat(string nome, string tipo, string? grupo = null, int ordem = 0) =>
        new() { Id = Guid.NewGuid(), Nome = nome, Tipo = tipo, Grupo = grupo, Ordem = ordem, Ativa = true };

    [Fact]
    public void CalcularPeriodo_SemCategorias_EbitdaNull()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m) },
            new() { Item("Aluguel", 300m) });
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg });
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
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg });
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
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg });
        Assert.NotNull(resultado.Ebitda);
        Assert.Equal(1000m, resultado.Ebitda!.Valor);
    }

    [Fact]
    public void CalcularPeriodo_SemSalariosEInsumos_PrimeCostNull()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new() { Item("Aluguel", 300m, "Aluguel", "CustoFixo") });
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg });
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
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg });
        Assert.NotNull(resultado.PrimeCost);
        Assert.Equal(0.7m, resultado.PrimeCost!.Percentual);
        Assert.Equal("amarelo", resultado.PrimeCost.Semaforo);
    }

    [Fact]
    public void CalcularPeriodo_SemCategoria_PontoDeEquilibrioNull()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m) },
            new() { Item("Aluguel", 300m) });
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg });
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
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg });
        Assert.NotNull(resultado.PontoDeEquilibrio);
        Assert.Equal(375m, resultado.PontoDeEquilibrio!.Valor);
        Assert.Equal("verde", resultado.PontoDeEquilibrio.Semaforo);
    }

    [Fact]
    public void CalcularPeriodo_ReceitaZero_NaoDividePorZero()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1), new(), new());
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg });
        Assert.Null(resultado.Ebitda);
        Assert.Null(resultado.PrimeCost);
        Assert.Null(resultado.PontoDeEquilibrio);
    }

    [Fact]
    public void CalcularEvolucao_RetornaMesesCorretos()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
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

    [Fact]
    public void CalcularFluxoProjetado_SemContasFuturas_SaldoConstante()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var registro = CriarRegistro(hoje, new(), new(), saldoFinal: 1000m);
        var resultado = _sut.CalcularFluxoProjetado(new() { registro }, new(), 3);
        Assert.Equal(1000m, resultado.SaldoAtual);
        Assert.Equal(3, resultado.Dias.Count);
        Assert.All(resultado.Dias, d => Assert.Equal(1000m, d.SaldoProjetado));
    }

    // ---- Valuation ----

    [Fact]
    public void CalcularPeriodo_TresMesesDeDados_CalculaValuation()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var registros = new List<RegistroDiario>();
        for (int i = 2; i >= 0; i--)
        {
            var data = hoje.AddMonths(-i).AddDays(-5);
            registros.Add(CriarRegistro(data,
                new() { Item("Venda", 2000m, "Vendas", "Receita") },
                new() { Item("Custo", 1000m, "Aluguel", "CustoFixo") }));
        }

        var resultado = _sut.CalcularPeriodo(registros, registros);

        Assert.NotNull(resultado.Valuation);
        Assert.True(resultado.Valuation!.Valor > 0);
    }

    [Fact]
    public void CalcularPeriodo_SaldoZero_RunwayRetornaZero()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var registro = CriarRegistro(hoje,
            new() { Item("Venda", 500m, "Vendas", "Receita") },
            new() { Item("Custo", 1000m, "Aluguel", "CustoFixo") },
            saldoFinal: 0m);

        var resultado = _sut.CalcularPeriodo(new() { registro }, new() { registro });

        Assert.NotNull(resultado.Runway);
        Assert.Equal(0m, resultado.Runway!.Meses);
    }

    [Fact]
    public void CalcularPeriodo_SemContasPagarProximos30Dias_LiquidezAltaLiquidez()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var registro = CriarRegistro(hoje, new(), new(), saldoFinal: 5000m);

        var resultado = _sut.CalcularPeriodo(new() { registro }, new() { registro });

        Assert.NotNull(resultado.Liquidez);
        Assert.True(resultado.Liquidez!.AltaLiquidez);
    }

    [Fact]
    public void CalcularPeriodo_ComContasPagarProximos30Dias_CalculaLiquidez()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var amanha = hoje.AddDays(1);
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Data = hoje,
            Entradas = new(), Saidas = new(), SaldoFinal = 3000m,
            ContasReceber = new(),
            ContasPagar = new() { new() { Descricao = "Aluguel", Valor = 1000m, DataVencimento = amanha, Pago = false } },
        };

        var resultado = _sut.CalcularPeriodo(new() { registro }, new() { registro });

        Assert.NotNull(resultado.Liquidez);
        Assert.Equal(3.0m, resultado.Liquidez!.Indice);
        Assert.Equal("verde", resultado.Liquidez.Semaforo);
        Assert.False(resultado.Liquidez.AltaLiquidez);
    }

    [Fact]
    public void CalcularPeriodo_ComContasReceberProximos30Dias_SomaNoNumeradorLiquidez()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var amanha = hoje.AddDays(1);
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Data = hoje,
            Entradas = new(), Saidas = new(), SaldoFinal = 1000m,
            ContasReceber = new() { new() { Descricao = "Cliente X", Valor = 2000m, DataVencimento = amanha, Pago = false } },
            ContasPagar = new() { new() { Descricao = "Aluguel", Valor = 1000m, DataVencimento = amanha, Pago = false } },
        };
        var resultado = _sut.CalcularPeriodo(new() { registro }, new() { registro });
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
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg });
        Assert.NotNull(resultado.TicketMedio);
        Assert.Equal(2, resultado.TicketMedio!.QuantidadeRecebimentos);
        Assert.Equal(500m, resultado.TicketMedio.Valor);
    }

    [Fact]
    public void CalcularPeriodo_SemRecebimentos_TicketMedioNull()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1), new(), new() { Item("Aluguel", 300m, "Aluguel", "CustoFixo") });
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg });
        Assert.Null(resultado.TicketMedio);
    }

    // ---- Múltiplo Valuation ----

    [Fact]
    public void CalcularPeriodo_MultiploCustomizado_AplicaNoValuation()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var registros = new List<RegistroDiario>();
        for (int i = 2; i >= 0; i--)
            registros.Add(CriarRegistro(hoje.AddMonths(-i).AddDays(-5),
                new() { Item("Venda", 2000m, "Vendas", "Receita") },
                new() { Item("Custo", 1000m, "Aluguel", "CustoFixo") }));

        var v3 = _sut.CalcularPeriodo(registros, registros, 3m).Valuation!.Valor;
        var v6 = _sut.CalcularPeriodo(registros, registros, 6m).Valuation!.Valor;
        Assert.Equal(v3 * 2, v6);
    }

    // ---- Burn Rate ----

    [Fact]
    public void CalcularPeriodo_ComSaidas3Meses_CalculaBurnRate()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(hoje, new() { Item("Venda", 100m, "Vendas", "Receita") },
                new() { Item("Custo", 900m, "Aluguel", "CustoFixo") }, saldoFinal: 1000m),
        };
        var resultado = _sut.CalcularPeriodo(registros, registros);
        Assert.NotNull(resultado.BurnRate);
        Assert.Equal(900m, resultado.BurnRate);
    }

    // ---- CalcularIndicadores ----

    [Fact]
    public void CalcularIndicadores_FixoVariavelNaoClassificado_ReconciliaComTotalDespesas()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
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
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
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
    public void CalcularIndicadores_CategoriaComHistoricoAnterior_CalculaMediaEVariacaoPositiva()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
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
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var reg = CriarRegistro(hoje, new(), new() { Item("Software", 80m, "Software", "CustoFixo") });

        var resultado = _sut.CalcularIndicadores(new() { reg });

        var categoria = Assert.Single(resultado.RankingCategorias);
        Assert.Null(categoria.MediaMesesAnteriores);
        Assert.Null(categoria.VariacaoPercentual);
    }

    // ---- Ponto de Equilíbrio ----

    [Fact]
    public void CalcularIndicadores_PontoEquilibrio_CalculaValorMensalDiaUtilEDistancia()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
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
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
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
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var reg = CriarRegistro(hoje, new(), new() { Item("Aluguel", 300m, "Aluguel", "CustoFixo") });

        var resultado = _sut.CalcularIndicadores(new() { reg }).PontoEquilibrio;

        Assert.False(resultado.Disponivel);
        Assert.NotNull(resultado.MotivoIndisponivel);
    }

    // ---- Fôlego de Caixa ----

    [Fact]
    public void CalcularIndicadores_FolegoCaixa_CalculaMesesEFaixaConfortavel()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
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

        var resultado = _sut.CalcularIndicadores(registros).FolegoCaixa;

        Assert.True(resultado.Disponivel);
        Assert.Equal(1000m, resultado.CustoFixoMedioMensal);
        Assert.Equal(5000m, resultado.SaldoDisponivel); // registro mais recente (mês atual) da conta
        Assert.Equal(5.0m, resultado.Meses);
        Assert.Equal("confortavel", resultado.Faixa);
    }

    [Fact]
    public void CalcularIndicadores_FolegoCaixa_SaldoBaixo_FaixaCritica()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var contaId = Guid.NewGuid();
        var reg = CriarRegistro(hoje,
            new() { Item("Venda", 2000m, "Vendas", "Receita") },
            new() { Item("Aluguel", 1000m, "Aluguel", "CustoFixo") },
            saldoFinal: 500m);
        reg.ContaBancariaId = contaId;

        var resultado = _sut.CalcularIndicadores(new() { reg }).FolegoCaixa;

        Assert.True(resultado.Disponivel);
        Assert.Equal(0.5m, resultado.Meses);
        Assert.Equal("critico", resultado.Faixa);
    }

    [Fact]
    public void CalcularIndicadores_FolegoCaixa_SemCustoFixo_Indisponivel()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
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
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
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
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
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
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
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
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
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
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
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
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var reg = CriarRegistro(hoje, new() { Item("Venda", 500m, "Vendas", "Receita") }, new());

        var resultado = _sut.CalcularIndicadores(new() { reg }, mesesEvolucao: 6);

        Assert.Null(resultado.VariacaoReceitaAnoAnterior);
    }

    [Fact]
    public void CalcularIndicadores_ComMesAnteriorComReceita_CalculaVariacaoMesAMes()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(hoje, new() { Item("Venda", 1200m, "Vendas", "Receita") }, new()),
            CriarRegistro(hoje.AddMonths(-1), new() { Item("Venda", 1000m, "Vendas", "Receita") }, new()),
        };

        var resultado = _sut.CalcularIndicadores(registros, mesesEvolucao: 6);

        Assert.Equal(20.0m, resultado.VariacaoReceitaMesAnterior);
    }

    [Fact]
    public void CalcularIndicadores_MesAnteriorSemReceita_VariacaoMesAMesNula()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var reg = CriarRegistro(hoje, new() { Item("Venda", 1200m, "Vendas", "Receita") }, new());

        var resultado = _sut.CalcularIndicadores(new() { reg }, mesesEvolucao: 6);

        Assert.Null(resultado.VariacaoReceitaMesAnterior);
    }

    [Fact]
    public void CalcularIndicadores_Com13MesesEMesmoMesAnoAnterior_CalculaVariacaoAnoAnterior()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
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
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
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
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg });
        Assert.Equal("amarelo", resultado.Ebitda!.Semaforo);
    }

    [Fact]
    public void CalcularPeriodo_EbitdaAbaixoDe5Porcento_SemaforoVermelho()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new() { Item("Custo", 980m, "Aluguel", "CustoFixo") });
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg });
        Assert.Equal("vermelho", resultado.Ebitda!.Semaforo);
    }

    // ---- Semáforos: PrimeCost ----

    [Fact]
    public void CalcularPeriodo_PrimeCostAbaixoDe60Porcento_SemaforoVerde()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new() { Item("Salarios", 500m, "Salários/Folha", "CustoFixo") });
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg });
        Assert.Equal("verde", resultado.PrimeCost!.Semaforo);
    }

    [Fact]
    public void CalcularPeriodo_PrimeCostAcimaDe75Porcento_SemaforoVermelho()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new() { Item("Salarios", 800m, "Salários/Folha", "CustoFixo") });
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg });
        Assert.Equal("vermelho", resultado.PrimeCost!.Semaforo);
    }

    // ---- Semáforos: Ponto de Equilíbrio ----

    [Fact]
    public void CalcularPeriodo_ReceitaAbaixoDoPontoDeEquilibrio_SemaforoAmarelo()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new() { Item("Custo", 900m, "Aluguel", "CustoFixo") });
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg });
        Assert.Equal(900m, resultado.PontoDeEquilibrio!.Valor);
        Assert.Equal("amarelo", resultado.PontoDeEquilibrio.Semaforo);
    }

    [Fact]
    public void CalcularPeriodo_ReceitaMuitoAbaixoDoPontoDeEquilibrio_SemaforoVermelho()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new() { Item("Custo", 1200m, "Aluguel", "CustoFixo") });
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg });
        Assert.Equal("vermelho", resultado.PontoDeEquilibrio!.Semaforo);
    }

    [Fact]
    public void CalcularPeriodo_MargemContribuicaoZeroOuNegativa_PontoDeEquilibrioZeroESemaforoVerde()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 1000m, "Vendas", "Receita") },
            new()
            {
                Item("Custo Fixo", 100m, "Aluguel", "CustoFixo"),
                Item("Custo Variavel", 1000m, "Insumos/Mercadoria", "CustoVariavel"),
            });
        var resultado = _sut.CalcularPeriodo(new() { reg }, new() { reg });
        Assert.Equal(0m, resultado.PontoDeEquilibrio!.Valor);
        Assert.Equal("verde", resultado.PontoDeEquilibrio.Semaforo);
    }

    // ---- Semáforos: Valuation ----

    [Fact]
    public void CalcularPeriodo_LucroAnteriorZero_ValuationSemaforoCinza()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(hoje, new() { Item("Venda", 1000m, "Vendas", "Receita") }, new() { Item("Custo", 500m, "Aluguel", "CustoFixo") }),
            CriarRegistro(hoje.AddMonths(-1), new() { Item("Venda", 500m, "Vendas", "Receita") }, new() { Item("Custo", 500m, "Aluguel", "CustoFixo") }),
        };

        var resultado = _sut.CalcularPeriodo(registros, registros);

        Assert.Equal("cinza", resultado.Valuation!.Semaforo);
    }

    [Fact]
    public void CalcularPeriodo_LucroSubiuMaisDe5Porcento_ValuationSemaforoVerde()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(hoje, new() { Item("Venda", 1200m, "Vendas", "Receita") }, new()),
            CriarRegistro(hoje.AddMonths(-1), new() { Item("Venda", 1000m, "Vendas", "Receita") }, new()),
        };

        var resultado = _sut.CalcularPeriodo(registros, registros);

        Assert.Equal("verde", resultado.Valuation!.Semaforo);
    }

    [Fact]
    public void CalcularPeriodo_LucroCaiuMaisDe5Porcento_ValuationSemaforoVermelho()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(hoje, new() { Item("Venda", 800m, "Vendas", "Receita") }, new()),
            CriarRegistro(hoje.AddMonths(-1), new() { Item("Venda", 1000m, "Vendas", "Receita") }, new()),
        };

        var resultado = _sut.CalcularPeriodo(registros, registros);

        Assert.Equal("vermelho", resultado.Valuation!.Semaforo);
    }

    [Fact]
    public void CalcularPeriodo_LucroEstavel_ValuationSemaforoAmarelo()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(hoje, new() { Item("Venda", 1020m, "Vendas", "Receita") }, new()),
            CriarRegistro(hoje.AddMonths(-1), new() { Item("Venda", 1000m, "Vendas", "Receita") }, new()),
        };

        var resultado = _sut.CalcularPeriodo(registros, registros);

        Assert.Equal("amarelo", resultado.Valuation!.Semaforo);
    }

    // ---- Semáforos: Runway ----

    [Fact]
    public void CalcularPeriodo_SemGastosNosUltimos3Meses_RunwaySemaforoCinza()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var registro = CriarRegistro(hoje, new() { Item("Venda", 500m, "Vendas", "Receita") }, new(), saldoFinal: 1000m);

        var resultado = _sut.CalcularPeriodo(new() { registro }, new() { registro });

        Assert.Equal("cinza", resultado.Runway!.Semaforo);
    }

    [Fact]
    public void CalcularPeriodo_RunwayMaiorQue6Meses_SemaforoVerde()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var registro = CriarRegistro(hoje, new(), new() { Item("Custo", 100m, "Aluguel", "CustoFixo") }, saldoFinal: 800m);

        var resultado = _sut.CalcularPeriodo(new() { registro }, new() { registro });

        Assert.Equal(8.0m, resultado.Runway!.Meses);
        Assert.Equal("verde", resultado.Runway.Semaforo);
    }

    [Fact]
    public void CalcularPeriodo_RunwayEntre3e6Meses_SemaforoAmarelo()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var registro = CriarRegistro(hoje, new(), new() { Item("Custo", 100m, "Aluguel", "CustoFixo") }, saldoFinal: 400m);

        var resultado = _sut.CalcularPeriodo(new() { registro }, new() { registro });

        Assert.Equal(4.0m, resultado.Runway!.Meses);
        Assert.Equal("amarelo", resultado.Runway.Semaforo);
    }

    [Fact]
    public void CalcularPeriodo_RunwayMenorQue3Meses_SemaforoVermelho()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var registro = CriarRegistro(hoje, new(), new() { Item("Custo", 100m, "Aluguel", "CustoFixo") }, saldoFinal: 100m);

        var resultado = _sut.CalcularPeriodo(new() { registro }, new() { registro });

        Assert.Equal(1.0m, resultado.Runway!.Meses);
        Assert.Equal("vermelho", resultado.Runway.Semaforo);
    }

    // ---- Semáforos: Liquidez ----

    [Fact]
    public void CalcularPeriodo_IndiceLiquidezEntre1e15_SemaforoAmarelo()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var amanha = hoje.AddDays(1);
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Data = hoje,
            Entradas = new(), Saidas = new(), SaldoFinal = 1200m,
            ContasReceber = new(),
            ContasPagar = new() { new() { Descricao = "Fornecedor", Valor = 1000m, DataVencimento = amanha, Pago = false } },
        };

        var resultado = _sut.CalcularPeriodo(new() { registro }, new() { registro });

        Assert.Equal(1.2m, resultado.Liquidez!.Indice);
        Assert.Equal("amarelo", resultado.Liquidez.Semaforo);
    }

    [Fact]
    public void CalcularPeriodo_IndiceLiquidezAbaixoDe1_SemaforoVermelho()
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var amanha = hoje.AddDays(1);
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Data = hoje,
            Entradas = new(), Saidas = new(), SaldoFinal = 300m,
            ContasReceber = new(),
            ContasPagar = new() { new() { Descricao = "Fornecedor", Valor = 1000m, DataVencimento = amanha, Pago = false } },
        };

        var resultado = _sut.CalcularPeriodo(new() { registro }, new() { registro });

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

        Assert.Equal(400m, dre.DespesasFixas.Total);
        Assert.Equal(260m, dre.ResultadoOperacional);
        Assert.Equal(26.0m, dre.ResultadoOperacionalPercentual);

        Assert.Equal(30m, dre.DespesasNaoOperacionais.Total);
        Assert.Equal(10m, dre.NaoClassificado.Total);
        Assert.Equal("Não Classificado", Assert.Single(dre.NaoClassificado.Categorias).Nome);

        Assert.Equal(220m, dre.ResultadoLiquido);
        Assert.Equal(22.0m, dre.ResultadoLiquidoPercentual);

        // Reconciliação: a soma das 5 linhas da análise vertical bate com o total de despesas de sempre.
        Assert.Equal(dre.TotalDespesas, dre.Deducoes.Total + dre.CustosVariaveis.Total + dre.DespesasFixas.Total
            + dre.DespesasNaoOperacionais.Total + dre.NaoClassificado.Total);
        Assert.Equal(dre.Resultado, dre.ResultadoLiquido);
    }

    [Fact]
    public void CalcularDre_CategoriaNaoCadastradaNoPlanoDeContas_VaiParaNaoClassificado()
    {
        var reg = CriarRegistro(new DateOnly(2026, 6, 1),
            new() { Item("Venda", 500m, "Vendas", "Receita") },
            new() { Item("Gasto estranho", 50m, "Categoria Removida", "CustoFixo") });

        var dre = _sut.CalcularDre(new() { reg }, _planoDeContasTeste);

        Assert.Equal(0m, dre.DespesasFixas.Total);
        Assert.Equal(50m, dre.NaoClassificado.Total);
        Assert.Equal("Categoria Removida", Assert.Single(dre.NaoClassificado.Categorias).Nome);
    }

    [Fact]
    public void CalcularDre_IgnoraTransferenciasERendimento_NaoEntramNemComoNaoClassificado()
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
                Item("Rendimento negativo", 30m, "Rendimento", "Rendimento"),
            });

        var dre = _sut.CalcularDre(new() { reg }, _planoDeContasTeste);

        Assert.Equal(1000m, dre.ReceitaBruta); // resgate (transferência) não entra na receita
        Assert.Equal(400m, dre.TotalDespesas); // só o aluguel — transferência e rendimento ficam de fora
        Assert.Equal(0m, dre.NaoClassificado.Total); // não viram "Não Classificado": são excluídos, não desconhecidos
        Assert.Equal(600m, dre.Resultado);
    }

    [Fact]
    public void CalcularDre_ComRendimento_MostraReceitaFinanceiraSeparadaEForaDaReceitaOperacional()
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

        Assert.Equal(1000m, dre.ReceitaBruta); // rendimento não entra na receita operacional
        Assert.Equal(600m, dre.ResultadoOperacional); // 1000 - 400, sem o rendimento
        Assert.Equal(80m, dre.ReceitaFinanceira.Total); // 100 (rendimento) - 20 (rendimento negativo)
        Assert.Equal(680m, dre.ResultadoLiquido); // 600 (operacional) + 80 (financeira)
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
}
