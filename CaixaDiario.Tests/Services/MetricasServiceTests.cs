using CaixaDiario.API.Models;
using CaixaDiario.API.Services;

namespace CaixaDiario.Tests.Services;

public class MetricasServiceTests
{
    private readonly MetricasService _sut = new();

    private static RegistroDiario CriarRegistro(DateOnly data, List<ItemFinanceiro> entradas, List<ItemFinanceiro> saidas, decimal saldoFinal = 0) =>
        new() { Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Data = data, Entradas = entradas, Saidas = saidas, SaldoFinal = saldoFinal };

    private static ItemFinanceiro Item(string desc, decimal valor, string? categoria = null, string? tipoCusto = null) =>
        new() { Descricao = desc, Valor = valor, Categoria = categoria, TipoCusto = tipoCusto };

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
}
