using CaixaDiario.API.DTOs.Financiamentos;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;

namespace CaixaDiario.API.Services;

/// <summary>
/// Fase 6.2/6.3: matemática de amortização (Price e SAC) e taxa implícita. Pura — sem acesso a
/// repositório/banco, só números. O solver de taxa roda em double (método iterativo, instável em
/// decimal); todo valor monetário devolvido já é decimal arredondado a centavos.
/// </summary>
public static class CalculadoraAmortizacao
{
    private const int MaxIteracoes = 200;
    private const double Tolerancia = 1e-12;

    /// <summary>
    /// Taxa mensal implícita do sistema Price dado valor financiado, valor da parcela e nº de
    /// parcelas — Newton-Raphson sobre f(i) = PV·i/(1-(1+i)^-n) - PMT. Não arredonda: a tabela de
    /// amortização usa essa taxa "cheia" pra não acumular erro — só o valor de cada parcela é
    /// arredondado a centavos.
    /// </summary>
    public static decimal ResolverTaxaImplicitaPrice(decimal valorFinanciado, decimal valorParcela, int numeroParcelas)
    {
        if (valorFinanciado <= 0)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Valor financiado deve ser maior que zero.", "valorFinanciado");
        if (valorParcela <= 0)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Valor da parcela deve ser maior que zero.", "valorParcela");
        if (numeroParcelas <= 0)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Número de parcelas deve ser maior que zero.", "numeroParcelas");
        if (valorParcela * numeroParcelas <= valorFinanciado)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS,
                "A soma das parcelas precisa ser maior que o valor financiado — confira o valor da parcela.", "valorParcela");

        var pv = (double)valorFinanciado;
        var pmt = (double)valorParcela;
        var n = numeroParcelas;
        var i = 0.02;

        for (var iter = 0; iter < MaxIteracoes; iter++)
        {
            var f = Pmt(pv, i, n) - pmt;
            const double h = 1e-7;
            var f2 = Pmt(pv, i + h, n) - pmt;
            var df = (f2 - f) / h;
            var iNovo = i - f / df;
            if (Math.Abs(iNovo - i) < Tolerancia) { i = iNovo; break; }
            i = iNovo;
        }

        return (decimal)i;
    }

    /// <summary>Tabela de amortização pelo sistema Price (parcela fixa, juros decrescente,
    /// amortização crescente) — a última parcela absorve o resto do arredondamento, o saldo
    /// devedor sempre termina em exatamente zero.</summary>
    public static List<ParcelaAmortizacaoDto> GerarTabelaPrice(
        decimal valorFinanciado, decimal taxaMensal, int numeroParcelas, DateOnly dataPrimeiraParcela)
    {
        var pmt = Math.Round((decimal)Pmt((double)valorFinanciado, (double)taxaMensal, numeroParcelas), 2);
        var parcelas = new List<ParcelaAmortizacaoDto>();
        var saldo = valorFinanciado;

        for (var i = 1; i <= numeroParcelas; i++)
        {
            var juros = Math.Round(saldo * taxaMensal, 2);
            decimal amortizacao, valor;
            if (i == numeroParcelas)
            {
                // Última parcela: amortização = o que resta do saldo, nunca o valor "de tabela" —
                // garante saldo final exatamente 0,00 mesmo com arredondamento acumulado nas outras.
                amortizacao = saldo;
                valor = amortizacao + juros;
            }
            else
            {
                amortizacao = pmt - juros;
                valor = pmt;
            }
            saldo -= amortizacao;
            parcelas.Add(new ParcelaAmortizacaoDto
            {
                Numero = i, Vencimento = dataPrimeiraParcela.AddMonths(i - 1),
                Valor = valor, Juros = juros, Amortizacao = amortizacao, SaldoDevedor = Math.Round(saldo, 2),
            });
        }

        return parcelas;
    }

    /// <summary>Tabela de amortização pelo sistema SAC (amortização fixa, juros e parcela
    /// decrescentes) — mesma garantia de saldo final exatamente zero na última parcela.</summary>
    public static List<ParcelaAmortizacaoDto> GerarTabelaSac(
        decimal valorFinanciado, decimal taxaMensal, int numeroParcelas, DateOnly dataPrimeiraParcela)
    {
        var amortizacaoFixa = Math.Round(valorFinanciado / numeroParcelas, 2);
        var parcelas = new List<ParcelaAmortizacaoDto>();
        var saldo = valorFinanciado;
        var amortizadoAteAgora = 0m;

        for (var i = 1; i <= numeroParcelas; i++)
        {
            var juros = Math.Round(saldo * taxaMensal, 2);
            var amortizacao = i == numeroParcelas ? valorFinanciado - amortizadoAteAgora : amortizacaoFixa;
            amortizadoAteAgora += amortizacao;
            saldo -= amortizacao;
            parcelas.Add(new ParcelaAmortizacaoDto
            {
                Numero = i, Vencimento = dataPrimeiraParcela.AddMonths(i - 1),
                Valor = amortizacao + juros, Juros = juros, Amortizacao = amortizacao, SaldoDevedor = Math.Round(saldo, 2),
            });
        }

        return parcelas;
    }

    // Sem juros (SemJuros): parcela fixa = valor/n, sem componente de juros nenhuma.
    public static List<ParcelaAmortizacaoDto> GerarTabelaSemJuros(
        decimal valorFinanciado, int numeroParcelas, DateOnly dataPrimeiraParcela) =>
        GerarTabelaPrice(valorFinanciado, 0m, numeroParcelas, dataPrimeiraParcela);

    private static double Pmt(double pv, double i, int n) =>
        i == 0 ? pv / n : pv * i / (1 - Math.Pow(1 + i, -n));
}
