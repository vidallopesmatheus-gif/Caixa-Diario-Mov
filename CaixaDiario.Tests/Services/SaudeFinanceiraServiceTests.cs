using CaixaDiario.API.Models;
using CaixaDiario.API.Services;

namespace CaixaDiario.Tests.Services;

public class SaudeFinanceiraServiceTests
{
    private readonly SaudeFinanceiraService _sut = new();
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
    public void Calcular_ComReceitaEDespesasDoMes_CalculaTaxaPoupancaVerde()
    {
        var registros = new List<RegistroDiario> { CriarRegistro(Hoje, entradas: 1000m, saidas: 700m) };

        var resultado = _sut.Calcular(registros, new List<ContaRecorrente>(), new List<MetaAnual>());

        Assert.True(resultado.TaxaPoupanca.Disponivel);
        Assert.Equal(30m, resultado.TaxaPoupanca.Valor);
        Assert.Equal("verde", resultado.TaxaPoupanca.Semaforo);
    }

    [Fact]
    public void Calcular_SemReceitaNoMes_TaxaPoupancaIndisponivel()
    {
        var resultado = _sut.Calcular(new List<RegistroDiario>(), new List<ContaRecorrente>(), new List<MetaAnual>());

        Assert.False(resultado.TaxaPoupanca.Disponivel);
        Assert.Equal("cinza", resultado.TaxaPoupanca.Semaforo);
    }

    [Fact]
    public void Calcular_IgnoraRegistrosExcluidos()
    {
        var excluido = CriarRegistro(Hoje, entradas: 1000m, saidas: 100m);
        excluido.Excluido = true;

        var resultado = _sut.Calcular(new List<RegistroDiario> { excluido }, new List<ContaRecorrente>(), new List<MetaAnual>());

        Assert.False(resultado.TaxaPoupanca.Disponivel);
    }

    [Theory]
    [InlineData(1000, 850, "amarelo")]
    [InlineData(1000, 980, "vermelho")]
    public void Calcular_TaxaPoupanca_ClassificaSemaforoPorFaixa(decimal receita, decimal despesa, string semaforoEsperado)
    {
        var registros = new List<RegistroDiario> { CriarRegistro(Hoje, entradas: receita, saidas: despesa) };

        var resultado = _sut.Calcular(registros, new List<ContaRecorrente>(), new List<MetaAnual>());

        Assert.Equal(semaforoEsperado, resultado.TaxaPoupanca.Semaforo);
    }

    [Fact]
    public void Calcular_SemHistoricoDeReceita_ComprometimentoFixoIndisponivel()
    {
        var resultado = _sut.Calcular(new List<RegistroDiario>(), new List<ContaRecorrente>(), new List<MetaAnual>());

        Assert.False(resultado.ComprometimentoFixos.Disponivel);
    }

    [Fact]
    public void Calcular_ComContasAPagarERecorrencia_CalculaComprometimentoFixoVerde()
    {
        var mesPassado1 = Hoje.AddMonths(-1);
        var mesPassado2 = Hoje.AddMonths(-2);
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(mesPassado1, entradas: 5000m),
            CriarRegistro(mesPassado2, entradas: 5000m),
        };

        var registroComPagar = CriarRegistro(Hoje);
        registroComPagar.ContasPagar.Add(new ContaProvisionada
        {
            Descricao = "Fornecedor", Valor = 800m, Pago = false,
            DataVencimento = new DateOnly(Hoje.Year, Hoje.Month, Math.Min(15, DateTime.DaysInMonth(Hoje.Year, Hoje.Month))),
        });
        registros.Add(registroComPagar);

        var recorrencia = new ContaRecorrente
        {
            Id = Guid.NewGuid(),
            ClienteId = Guid.NewGuid(),
            Descricao = "Aluguel",
            Valor = 200m,
            Tipo = "Pagar",
            Ativo = true,
            Periodicidade = "Mensal",
            DataInicio = new DateOnly(Hoje.Year - 1, Hoje.Month, 5),
            CriadoEm = DateTime.UtcNow,
        };

        var resultado = _sut.Calcular(registros, new List<ContaRecorrente> { recorrencia }, new List<MetaAnual>());

        Assert.True(resultado.ComprometimentoFixos.Disponivel);
        Assert.Equal(20m, resultado.ComprometimentoFixos.Valor);
        Assert.Equal("verde", resultado.ComprometimentoFixos.Semaforo);
    }

    [Fact]
    public void Calcular_SemMetaElegivel_RitmoMetaIndisponivel()
    {
        var metaSimples = new MetaAnual { Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), ModoMeta = "simples", AtualizadoEm = DateTime.UtcNow, CriadoEm = DateTime.UtcNow };

        var resultado = _sut.Calcular(new List<RegistroDiario>(), new List<ContaRecorrente>(), new List<MetaAnual> { metaSimples });

        Assert.False(resultado.RitmoMeta.Disponivel);
    }

    [Fact]
    public void Calcular_ComMenosDeUmMesDecorrido_RitmoMetaIndisponivel()
    {
        var meta = new MetaAnual
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), ModoMeta = "metodo",
            ValorSonho = 100000m, PrazoAnos = 5, TaxaRetorno = 10m, TotalInvestido = 0m,
            AtualizadoEm = DateTime.UtcNow, CriadoEm = DateTime.UtcNow,
        };

        var resultado = _sut.Calcular(new List<RegistroDiario>(), new List<ContaRecorrente>(), new List<MetaAnual> { meta });

        Assert.False(resultado.RitmoMeta.Disponivel);
    }

    [Fact]
    public void Calcular_ComMetaElegivelEProgressoParcial_CalculaRitmoAmarelo()
    {
        var meta = new MetaAnual
        {
            Id = Guid.NewGuid(),
            ClienteId = Guid.NewGuid(),
            Sonho = "Aposentadoria",
            ModoMeta = "metodo",
            ValorSonho = 120000m,
            PrazoAnos = 5,
            TaxaRetorno = 12m,
            TotalInvestido = 20000m,
            AtualizadoEm = DateTime.UtcNow.AddMonths(-6),
            CriadoEm = DateTime.UtcNow.AddMonths(-6),
        };

        var resultado = _sut.Calcular(new List<RegistroDiario>(), new List<ContaRecorrente>(), new List<MetaAnual> { meta });

        Assert.True(resultado.RitmoMeta.Disponivel);
        Assert.Equal(85.3m, resultado.RitmoMeta.Valor);
        Assert.Equal("amarelo", resultado.RitmoMeta.Semaforo);
        Assert.Contains("Aposentadoria", resultado.RitmoMeta.Calculo);
    }

    [Fact]
    public void Calcular_IgnoraTransferenciasERendimentoNaTaxaDePoupanca()
    {
        var registro = CriarRegistro(Hoje, entradas: 1000m, saidas: 700m);
        registro.Entradas.Add(new ItemFinanceiro { Descricao = "Resgate", Valor = 5000m, Categoria = "Transferência", TipoCusto = "Transferencia" });
        registro.Saidas.Add(new ItemFinanceiroSaida { Descricao = "Aporte", Valor = 2000m, Categoria = "Transferência", TipoCusto = "Transferencia" });
        registro.Entradas.Add(new ItemFinanceiro { Descricao = "Rendimento", Valor = 50m, Categoria = "Rendimento", TipoCusto = "Rendimento" });

        var resultado = _sut.Calcular(new List<RegistroDiario> { registro }, new List<ContaRecorrente>(), new List<MetaAnual>());

        // Mesmo resultado do teste acima (30%) — transferências/rendimento não entram na conta.
        Assert.Equal(30m, resultado.TaxaPoupanca.Valor);
    }
}
