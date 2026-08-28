using CaixaDiario.API.Models;
using CaixaDiario.API.Services;

namespace CaixaDiario.Tests.Services;

public class InsightServiceTests
{
    private readonly InsightService _sut = new();
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly int DiasDecorridos = Math.Max(1, Hoje.Day);
    private static readonly int DiasNoMes = DateTime.DaysInMonth(Hoje.Year, Hoje.Month);

    // Resolve o gasto/lucro do mês corrente necessário para que a extrapolação
    // (valor / diasDecorridos * diasNoMes) atinja exatamente o alvo desejado,
    // independente de em que dia do mês o teste rodar.
    private static decimal ValorParaExtrapolarPara(decimal alvo) => alvo * DiasDecorridos / DiasNoMes;

    private static RegistroDiario CriarRegistro(DateOnly data, decimal entradas = 0m, decimal saidas = 0m) => new()
    {
        Id = Guid.NewGuid(),
        ClienteId = Guid.NewGuid(),
        ContaBancariaId = Guid.NewGuid(),
        Data = data,
        Entradas = entradas != 0m ? new List<ItemFinanceiro> { new() { Descricao = "Receita", Valor = entradas } } : new(),
        Saidas = saidas != 0m ? new List<ItemFinanceiroSaida> { new() { Descricao = "Despesa", Valor = saidas, Categoria = "Geral" } } : new(),
        ContasReceber = new(),
        ContasPagar = new(),
        SaldoFinal = 0m,
        CriadoEm = DateTime.UtcNow,
        SalvoEm = DateTime.UtcNow,
    };

    private static MetaAnual CriarMetaAtrasada() => new()
    {
        Id = Guid.NewGuid(),
        ClienteId = Guid.NewGuid(),
        Sonho = "Casa na praia",
        ModoMeta = "metodo",
        ValorSonho = 120000m,
        PrazoAnos = 5,
        TaxaRetorno = 12m,
        TotalInvestido = 20000m,
        AtualizadoEm = DateTime.UtcNow.AddMonths(-6),
        CriadoEm = DateTime.UtcNow.AddMonths(-6),
    };

    [Fact]
    public void Calcular_SemDadosRelevantes_RetornaListaVazia()
    {
        var insights = _sut.Calcular(new List<RegistroDiario>(), new List<ContaRecorrente>(), null);

        Assert.Empty(insights);
    }

    [Fact]
    public void Calcular_ComSaldoProjetadoNegativo_GeraAlertaComDiasAte()
    {
        var registroAtual = CriarRegistro(Hoje);
        registroAtual.SaldoFinal = 200m;
        registroAtual.ContasPagar.Add(new ContaProvisionada
        {
            Descricao = "Fornecedor", Valor = 500m, Pago = false, DataVencimento = Hoje.AddDays(3),
        });

        var insights = _sut.Calcular(new List<RegistroDiario> { registroAtual }, new List<ContaRecorrente>(), null);

        var alerta = Assert.Single(insights, i => i.Prioridade == 1);
        Assert.Equal("alerta", alerta.Tipo);
        Assert.Contains("3 dia", alerta.Texto);
        Assert.Equal("saldo", alerta.Categoria);
    }

    [Fact]
    public void Calcular_ComGastoMuitoAcimaDaMedia_GeraAlertaSemMeta()
    {
        var mesPassado1 = Hoje.AddMonths(-1);
        var mesPassado2 = Hoje.AddMonths(-2);
        var mesPassado3 = Hoje.AddMonths(-3);
        var gastoAtual = ValorParaExtrapolarPara(1300m); // 30% acima da média de 1000

        var registros = new List<RegistroDiario>
        {
            CriarRegistro(mesPassado1, saidas: 1000m),
            CriarRegistro(mesPassado2, saidas: 1000m),
            CriarRegistro(mesPassado3, saidas: 1000m),
            CriarRegistro(Hoje, saidas: gastoAtual),
        };

        var insights = _sut.Calcular(registros, new List<ContaRecorrente>(), null);

        var alerta = Assert.Single(insights, i => i.Prioridade == 2);
        Assert.Equal("alerta", alerta.Tipo);
        Assert.Contains("acima da média", alerta.Texto);
        Assert.Equal("gasto", alerta.Categoria);
        Assert.DoesNotContain(insights, i => i.Prioridade == 3);
    }

    [Fact]
    public void Calcular_ComGastoAcimaDaMediaEMetaAtiva_GeraTambemAlertaDeImpactoNaMeta()
    {
        var mesPassado1 = Hoje.AddMonths(-1);
        var mesPassado2 = Hoje.AddMonths(-2);
        var mesPassado3 = Hoje.AddMonths(-3);
        var gastoAtual = ValorParaExtrapolarPara(1300m);

        var registros = new List<RegistroDiario>
        {
            CriarRegistro(mesPassado1, saidas: 1000m),
            CriarRegistro(mesPassado2, saidas: 1000m),
            CriarRegistro(mesPassado3, saidas: 1000m),
            CriarRegistro(Hoje, saidas: gastoAtual),
        };

        var insights = _sut.Calcular(registros, new List<ContaRecorrente>(), CriarMetaAtrasada());

        var impacto = Assert.Single(insights, i => i.Prioridade == 3 && i.Texto.Contains("excesso de gastos"));
        Assert.Equal("alerta", impacto.Tipo);
        Assert.Contains("Casa na praia", impacto.Texto);
        Assert.Equal("meta", impacto.Categoria);
    }

    [Fact]
    public void Calcular_ComGastoMuitoAbaixoDaMedia_GeraInsightPositivo()
    {
        var mesPassado1 = Hoje.AddMonths(-1);
        var mesPassado2 = Hoje.AddMonths(-2);
        var mesPassado3 = Hoje.AddMonths(-3);
        var gastoAtual = ValorParaExtrapolarPara(700m); // 30% abaixo da média de 1000

        var registros = new List<RegistroDiario>
        {
            CriarRegistro(mesPassado1, saidas: 1000m),
            CriarRegistro(mesPassado2, saidas: 1000m),
            CriarRegistro(mesPassado3, saidas: 1000m),
            CriarRegistro(Hoje, saidas: gastoAtual),
        };

        var insights = _sut.Calcular(registros, new List<ContaRecorrente>(), null);

        var positivo = Assert.Single(insights, i => i.Prioridade == 4 && i.Texto.Contains("controle"));
        Assert.Equal("positivo", positivo.Tipo);
        Assert.Equal("gasto", positivo.Categoria);
    }

    [Fact]
    public void Calcular_ComTransferenciaGrandeNoMes_NaoContaComoGastoNoInsight()
    {
        var mesPassado1 = Hoje.AddMonths(-1);
        var mesPassado2 = Hoje.AddMonths(-2);
        var mesPassado3 = Hoje.AddMonths(-3);
        var gastoOperacionalAtual = ValorParaExtrapolarPara(1000m); // igual à média — não deveria gerar alerta

        var registroAtual = CriarRegistro(Hoje, saidas: gastoOperacionalAtual);
        registroAtual.Saidas.Add(new ItemFinanceiroSaida
        {
            Descricao = "Transferência para investimento", Valor = 5000m,
            Categoria = "Transferência", TipoCusto = "Transferencia",
        });

        var registros = new List<RegistroDiario>
        {
            CriarRegistro(mesPassado1, saidas: 1000m),
            CriarRegistro(mesPassado2, saidas: 1000m),
            CriarRegistro(mesPassado3, saidas: 1000m),
            registroAtual,
        };

        var insights = _sut.Calcular(registros, new List<ContaRecorrente>(), null);

        Assert.DoesNotContain(insights, i => i.Categoria == "gasto");
    }

    [Fact]
    public void Calcular_ComRendimentoGrandeNoMes_NaoContaComoLucroNoInsight()
    {
        var mesPassado1 = Hoje.AddMonths(-1);
        var mesPassado2 = Hoje.AddMonths(-2);
        var mesPassado3 = Hoje.AddMonths(-3);
        var receitaOperacionalAtual = ValorParaExtrapolarPara(1000m); // igual à média — não deveria gerar insight

        var registroAtual = CriarRegistro(Hoje, entradas: receitaOperacionalAtual);
        registroAtual.Entradas.Add(new ItemFinanceiro
        {
            Descricao = "Rendimento CDI", Valor = 5000m, Categoria = "Rendimento", TipoCusto = "Rendimento",
        });

        var registros = new List<RegistroDiario>
        {
            CriarRegistro(mesPassado1, entradas: 1000m),
            CriarRegistro(mesPassado2, entradas: 1000m),
            CriarRegistro(mesPassado3, entradas: 1000m),
            registroAtual,
        };

        var insights = _sut.Calcular(registros, new List<ContaRecorrente>(), null);

        Assert.DoesNotContain(insights, i => i.Categoria == "lucro");
    }

    [Fact]
    public void Calcular_ComMetaEmAtrasoENenhumaAnomaliaDeGasto_GeraAlertaDeAtraso()
    {
        var insights = _sut.Calcular(new List<RegistroDiario>(), new List<ContaRecorrente>(), CriarMetaAtrasada());

        var alerta = Assert.Single(insights, i => i.Prioridade == 3);
        Assert.Equal("alerta", alerta.Tipo);
        Assert.Contains("atraso", alerta.Texto);
        Assert.Contains("Casa na praia", alerta.Texto);
        Assert.Equal("meta", alerta.Categoria);
    }

    [Fact]
    public void Calcular_ComMetaSemMetodoDefinido_NaoGeraInsightDeMeta()
    {
        var metaSimples = new MetaAnual
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), ModoMeta = "simples",
            AtualizadoEm = DateTime.UtcNow, CriadoEm = DateTime.UtcNow,
        };

        var insights = _sut.Calcular(new List<RegistroDiario>(), new List<ContaRecorrente>(), metaSimples);

        Assert.Empty(insights);
    }

    [Fact]
    public void Calcular_ComLucroMuitoAcimaDaMedia_GeraInsightPositivo()
    {
        var mesPassado1 = Hoje.AddMonths(-1);
        var mesPassado2 = Hoje.AddMonths(-2);
        var mesPassado3 = Hoje.AddMonths(-3);
        var lucroAtual = ValorParaExtrapolarPara(1200m); // 20% acima da média de 1000

        var registros = new List<RegistroDiario>
        {
            CriarRegistro(mesPassado1, entradas: 1000m),
            CriarRegistro(mesPassado2, entradas: 1000m),
            CriarRegistro(mesPassado3, entradas: 1000m),
            CriarRegistro(Hoje, entradas: lucroAtual),
        };

        var insights = _sut.Calcular(registros, new List<ContaRecorrente>(), null);

        var positivo = Assert.Single(insights, i => i.Prioridade == 5);
        Assert.Equal("positivo", positivo.Tipo);
        Assert.Contains("lucro", positivo.Texto);
        Assert.Equal("lucro", positivo.Categoria);
    }

    [Fact]
    public void Calcular_LimitaA5InsightsOrdenadosPorPrioridade()
    {
        var mesPassado1 = Hoje.AddMonths(-1);
        var mesPassado2 = Hoje.AddMonths(-2);
        var mesPassado3 = Hoje.AddMonths(-3);
        var gastoAtual = ValorParaExtrapolarPara(1300m);
        var lucroAtual = ValorParaExtrapolarPara(1200m);

        var registroAtual = CriarRegistro(Hoje, entradas: lucroAtual, saidas: gastoAtual);
        registroAtual.SaldoFinal = 200m;
        registroAtual.ContasPagar.Add(new ContaProvisionada
        {
            Descricao = "Fornecedor", Valor = 500m, Pago = false, DataVencimento = Hoje.AddDays(2),
        });

        var registros = new List<RegistroDiario>
        {
            CriarRegistro(mesPassado1, entradas: 1000m, saidas: 1000m),
            CriarRegistro(mesPassado2, entradas: 1000m, saidas: 1000m),
            CriarRegistro(mesPassado3, entradas: 1000m, saidas: 1000m),
            registroAtual,
        };

        var insights = _sut.Calcular(registros, new List<ContaRecorrente>(), CriarMetaAtrasada());

        Assert.True(insights.Count <= 5);
        Assert.True(insights.SequenceEqual(insights.OrderBy(i => i.Prioridade)));
    }
}
