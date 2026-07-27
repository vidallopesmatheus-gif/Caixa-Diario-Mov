using CaixaDiario.API.Models;
using CaixaDiario.API.Services;

namespace CaixaDiario.Tests.Services;

public class OrcamentoDinamicoServiceTests
{
    private readonly OrcamentoDinamicoService _sut = new();
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(DateTime.UtcNow);

    private static RegistroDiario CriarRegistro(DateOnly data, decimal entradas = 0m, decimal saidas = 0m) => new()
    {
        Id = Guid.NewGuid(),
        ClienteId = Guid.NewGuid(),
        ContaBancariaId = Guid.NewGuid(),
        Data = data,
        Entradas = entradas > 0 ? new List<ItemFinanceiro> { new() { Descricao = "Receita", Valor = entradas } } : new(),
        Saidas = saidas > 0 ? new List<ItemFinanceiroSaida> { new() { Descricao = "Despesa", Valor = saidas, Categoria = "Geral" } } : new(),
        ContasReceber = new(),
        ContasPagar = new(),
        CriadoEm = DateTime.UtcNow,
        SalvoEm = DateTime.UtcNow,
    };

    [Fact]
    public void Calcular_SemReceitaHistorica_RetornaTudoZeradoENaoUltrapassado()
    {
        var resultado = _sut.Calcular(new List<RegistroDiario>(), new List<ContaRecorrente>(), new List<MetaAnual>());

        Assert.Equal(0m, resultado.ReceitaEsperada);
        Assert.Equal(0m, resultado.CompromissosFixos);
        Assert.Equal(0m, resultado.AporteNecessario);
        Assert.Equal(0m, resultado.SaldoLivre);
        Assert.False(resultado.Ultrapassado);
        Assert.Equal(0m, resultado.PercentualUtilizado);
    }

    [Fact]
    public void Calcular_ComReceitaCompromissosEAporteDeMeta_CalculaSaldoLivreEPercentual()
    {
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(Hoje.AddMonths(-1), entradas: 5000m),
            CriarRegistro(Hoje.AddMonths(-2), entradas: 5000m),
            CriarRegistro(Hoje.AddMonths(-3), entradas: 5000m),
        };

        var registroComPagarEGasto = CriarRegistro(Hoje, saidas: 1000m);
        registroComPagarEGasto.ContasPagar.Add(new ContaProvisionada
        {
            Descricao = "Fornecedor", Valor = 800m, Pago = false,
            DataVencimento = new DateOnly(Hoje.Year, Hoje.Month, Math.Min(20, DateTime.DaysInMonth(Hoje.Year, Hoje.Month))),
        });
        registros.Add(registroComPagarEGasto);

        var recorrencia = new ContaRecorrente
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Descricao = "Assinatura", Valor = 200m,
            Tipo = "Pagar", Ativo = true, Periodicidade = "Mensal", DataInicio = new DateOnly(Hoje.Year - 1, Hoje.Month, 5),
            CriadoEm = DateTime.UtcNow,
        };

        var meta = new MetaAnual
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), ModoMeta = "metodo",
            ValorSonho = 120000m, PrazoAnos = 5, TaxaRetorno = 12m, TotalInvestido = 20000m,
            AtualizadoEm = DateTime.UtcNow, CriadoEm = DateTime.UtcNow,
        };

        var resultado = _sut.Calcular(registros, new List<ContaRecorrente> { recorrencia }, new List<MetaAnual> { meta });

        Assert.Equal(5000m, resultado.ReceitaEsperada);
        Assert.Equal(1000m, resultado.CompromissosFixos); // 800 (a pagar) + 200 (recorrência)
        Assert.Equal(1054.91m, resultado.AporteNecessario);
        Assert.Equal(2945.09m, resultado.SaldoLivre); // 5000 - 1000 - 1054.91
        Assert.Equal(1000m, resultado.GastoVariavelAtual);
        Assert.False(resultado.Ultrapassado);
        Assert.Equal(34.0m, resultado.PercentualUtilizado);
    }

    [Fact]
    public void Calcular_ComGastoVariavelAcimaDoSaldoLivre_MarcaUltrapassado()
    {
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(Hoje.AddMonths(-1), entradas: 1000m),
            CriarRegistro(Hoje, saidas: 1500m),
        };

        var resultado = _sut.Calcular(registros, new List<ContaRecorrente>(), new List<MetaAnual>());

        Assert.Equal(1000m, resultado.ReceitaEsperada);
        Assert.Equal(0m, resultado.CompromissosFixos);
        Assert.Equal(1000m, resultado.SaldoLivre);
        Assert.Equal(1500m, resultado.GastoVariavelAtual);
        Assert.True(resultado.Ultrapassado);
        Assert.Equal(150.0m, resultado.PercentualUtilizado);
    }

    [Fact]
    public void Calcular_IgnoraMetasSemModoMetodo()
    {
        var metaSimples = new MetaAnual
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), ModoMeta = "simples",
            ValorSonho = 50000m, PrazoAnos = 3, TaxaRetorno = 10m,
            AtualizadoEm = DateTime.UtcNow, CriadoEm = DateTime.UtcNow,
        };

        var resultado = _sut.Calcular(new List<RegistroDiario>(), new List<ContaRecorrente>(), new List<MetaAnual> { metaSimples });

        Assert.Equal(0m, resultado.AporteNecessario);
    }

    [Fact]
    public void Calcular_IgnoraRegistrosExcluidos()
    {
        var excluido = CriarRegistro(Hoje.AddMonths(-1), entradas: 5000m);
        excluido.Excluido = true;

        var resultado = _sut.Calcular(new List<RegistroDiario> { excluido }, new List<ContaRecorrente>(), new List<MetaAnual>());

        Assert.Equal(0m, resultado.ReceitaEsperada);
    }
}
