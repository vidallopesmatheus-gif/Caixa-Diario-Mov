using CaixaDiario.API.Models;
using CaixaDiario.API.Services;

namespace CaixaDiario.Tests.Services;

public class SaldoEncadeamentoTests
{
    private static readonly ContaBancaria Conta = new() { Id = Guid.NewGuid(), Nome = "Nubank", Tipo = "ContaCorrente", SaldoInicial = 1000m, Ativa = true };

    private static RegistroDiario Dia(DateOnly data, decimal entradas = 0m, decimal saidas = 0m, decimal inicioGravado = 0m, decimal finalGravado = 0m) => new()
    {
        Id = Guid.NewGuid(), ContaBancariaId = Conta.Id, Data = data, Inicio = inicioGravado, SaldoFinal = finalGravado,
        Entradas = entradas > 0 ? new() { new() { Descricao = "Entrada", Valor = entradas } } : new(),
        Saidas = saidas > 0 ? new() { new() { Descricao = "Saída", Valor = saidas } } : new(),
        ContasReceber = new(), ContasPagar = new(),
    };

    [Fact]
    public void Encadear_InicioGravadoErradoNoMeio_ReconstroiACadeiaDesdeOSaldoInicial()
    {
        var d1 = Dia(new DateOnly(2026, 10, 1), entradas: 500m, inicioGravado: 1000m, finalGravado: 1500m);
        var d2 = Dia(new DateOnly(2026, 10, 2), saidas: 200m, inicioGravado: 9999m, finalGravado: 9799m);
        var d3 = Dia(new DateOnly(2026, 10, 3), entradas: 50m, inicioGravado: 1300m, finalGravado: 1350m);

        var alterados = SaldoEncadeamento.Encadear(Conta.SaldoInicial, new[] { d3, d1, d2 });

        Assert.Equal((1500m, 1300m, 1300m, 1350m), (d2.Inicio, d2.SaldoFinal, d3.Inicio, d3.SaldoFinal));
        Assert.Equal(new[] { d2 }, alterados);
    }

    [Fact]
    public void EfeitoDoDia_BaixaSemLancamentoConta_MasParcelaDeFinanciamentoNao()
    {
        var dia = Dia(new DateOnly(2026, 10, 1), entradas: 100m);
        dia.ContasPagar.Add(new ContaProvisionada { Descricao = "Boleto antigo", Valor = 30m, Pago = true });
        dia.ContasPagar.Add(new ContaProvisionada { Descricao = "Boleto vinculado", Valor = 40m, Pago = true, LancamentoVinculadoId = Guid.NewGuid() });
        dia.ContasPagar.Add(new ContaProvisionada { Descricao = "Parcela 1/24", Valor = 900m, Pago = true, OrigemFinanciamentoId = Guid.NewGuid() });
        dia.ContasReceber.Add(new ContaProvisionada { Descricao = "Recebimento antigo", Valor = 20m, Pago = true, ValorRealizado = 25m });

        Assert.Equal(95m, SaldoEncadeamento.EfeitoDoDia(dia)); // 100 − 30 + 25
    }

    [Fact]
    public void SaldoAtual_ContaSemNenhumDia_ValeOSaldoInicial()
    {
        Assert.Equal(1000m, SaldoEncadeamento.SaldoAtual(Conta, new List<RegistroDiario>()));
    }

    [Fact]
    public void SaldoNoFimDe_DiaSemRegistro_UsaOUltimoDiaAnterior()
    {
        var dias = new List<RegistroDiario> { Dia(new DateOnly(2026, 10, 1), entradas: 500m), Dia(new DateOnly(2026, 10, 5), saidas: 100m) };
        SaldoEncadeamento.Encadear(Conta.SaldoInicial, dias);

        Assert.Equal(1500m, SaldoEncadeamento.SaldoNoFimDe(Conta, dias, new DateOnly(2026, 10, 3)));
    }
}
