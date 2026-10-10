using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Services;

namespace CaixaDiario.Tests.Services;

// Fase 6.2/6.3 — números exatos do documento de aceite, pra garantir que a fórmula de Price e o
// solver de taxa implícita batem com o que o cliente validou manualmente.
public class CalculadoraAmortizacaoTests
{
    [Fact]
    public void ResolverTaxaImplicitaPrice_30000Em42xDe1000_Calcula1Virgula6724PorcentoAoMes()
    {
        var taxa = CalculadoraAmortizacao.ResolverTaxaImplicitaPrice(30000m, 1000m, 42);

        Assert.Equal(1.6724m, Math.Round(taxa * 100, 4));
        var taxaAnual = (decimal)(Math.Pow(1 + (double)taxa, 12) - 1) * 100;
        Assert.Equal(22.0m, Math.Round(taxaAnual, 1));
    }

    [Fact]
    public void GerarTabelaPrice_30000Em42xDe1000_PrimeiraParcelaComJurosEAmortizacaoCorretos()
    {
        var taxa = CalculadoraAmortizacao.ResolverTaxaImplicitaPrice(30000m, 1000m, 42);
        var tabela = CalculadoraAmortizacao.GerarTabelaPrice(30000m, taxa, 42, new DateOnly(2026, 1, 10));

        var primeira = tabela[0];
        Assert.Equal(501.73m, primeira.Juros);
        Assert.Equal(498.27m, primeira.Amortizacao);
        Assert.Equal(new DateOnly(2026, 1, 10), primeira.Vencimento);
    }

    [Fact]
    public void GerarTabelaPrice_30000Em42xDe1000_JurosTotaisSaoDozeMil()
    {
        var taxa = CalculadoraAmortizacao.ResolverTaxaImplicitaPrice(30000m, 1000m, 42);
        var tabela = CalculadoraAmortizacao.GerarTabelaPrice(30000m, taxa, 42, new DateOnly(2026, 1, 10));

        Assert.Equal(12000m, Math.Round(tabela.Sum(p => p.Juros), 0));
        Assert.Equal(0m, tabela[^1].SaldoDevedor);
    }

    [Fact]
    public void GerarTabelaPrice_30000Em42xDe1000_SaldoEJurosAposA21aParcelaConferemComODocumento()
    {
        var taxa = CalculadoraAmortizacao.ResolverTaxaImplicitaPrice(30000m, 1000m, 42);
        var tabela = CalculadoraAmortizacao.GerarTabelaPrice(30000m, taxa, 42, new DateOnly(2026, 1, 10));

        var primeiras21 = tabela.Take(21).ToList();
        Assert.Equal(17586.20m, primeiras21[^1].SaldoDevedor);
        Assert.Equal(8586.20m, primeiras21.Sum(p => p.Juros));
    }

    [Fact]
    public void GerarTabelaPrice_60000Em48xA1Virgula5PorcentoAoMes_PrimeiraParcelaConfereComODocumento()
    {
        var tabela = CalculadoraAmortizacao.GerarTabelaPrice(60000m, 0.015m, 48, new DateOnly(2026, 1, 10));

        Assert.Equal(1762.50m, tabela[0].Valor);
        Assert.Equal(900.00m, tabela[0].Juros);
        Assert.Equal(862.50m, tabela[0].Amortizacao);
    }

    [Fact]
    public void GerarTabelaSac_AmortizacaoFixaJurosEParcelaDecrescentes()
    {
        var tabela = CalculadoraAmortizacao.GerarTabelaSac(12000m, 0.01m, 12, new DateOnly(2026, 1, 10));

        Assert.Equal(1000m, tabela[0].Amortizacao);
        Assert.Equal(1000m, tabela[^1].Amortizacao);
        Assert.Equal(120m, tabela[0].Juros); // 12000 * 1%
        Assert.True(tabela[0].Valor > tabela[^1].Valor); // parcela cai com o tempo
        Assert.True(tabela[0].Juros > tabela[1].Juros); // juros cai com o tempo (saldo menor)
        Assert.Equal(0m, tabela[^1].SaldoDevedor);
        Assert.Equal(12000m, tabela.Sum(p => p.Amortizacao));
    }

    [Fact]
    public void GerarTabelaSemJuros_ParcelaFixaSemComponenteDeJuros()
    {
        var tabela = CalculadoraAmortizacao.GerarTabelaSemJuros(1200m, 12, new DateOnly(2026, 1, 10));

        Assert.All(tabela, p => Assert.Equal(0m, p.Juros));
        Assert.Equal(100m, tabela[0].Valor);
        Assert.Equal(0m, tabela[^1].SaldoDevedor);
    }

    [Fact]
    public void ResolverTaxaImplicitaPrice_ParcelaNaoCobreOValorFinanciado_LancaDadosInvalidos()
    {
        var ex = Assert.Throws<ApiException>(() => CalculadoraAmortizacao.ResolverTaxaImplicitaPrice(30000m, 500m, 42));

        Assert.Equal(400, ex.StatusCode);
    }
}
