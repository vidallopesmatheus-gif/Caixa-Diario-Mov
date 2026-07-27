using CaixaDiario.API.Models;
using CaixaDiario.API.Services;

namespace CaixaDiario.Tests.Services;

public class ProjecaoServiceTests
{
    private readonly ProjecaoService _sut = new();
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(DateTime.UtcNow);

    private static RegistroDiario CriarRegistro(Guid contaId, DateOnly data, decimal saldoFinal) => new()
    {
        Id = Guid.NewGuid(),
        ClienteId = Guid.NewGuid(),
        ContaBancariaId = contaId,
        Data = data,
        Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
        SaldoFinal = saldoFinal,
        CriadoEm = DateTime.UtcNow,
        SalvoEm = DateTime.UtcNow,
    };

    [Fact]
    public void Calcular_SemFiltroDeConta_SomaUltimoSaldoDeCadaContaDistinta()
    {
        var contaA = Guid.NewGuid();
        var contaB = Guid.NewGuid();
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(contaA, Hoje.AddDays(-2), 800m),
            CriarRegistro(contaA, Hoje.AddDays(-1), 1000m), // mais recente da conta A
            CriarRegistro(contaB, Hoje.AddDays(-2), 500m),  // única/mais recente da conta B
        };

        var resultado = _sut.Calcular(registros, new List<ContaRecorrente>(), 5, null);

        Assert.Equal(1500m, resultado.SaldoAtual);
        Assert.Equal(5, resultado.TotalDias);
        Assert.Equal(5, resultado.Dias.Count);
    }

    [Fact]
    public void Calcular_ComFiltroDeConta_UsaApenasSaldoDaContaInformada()
    {
        var contaA = Guid.NewGuid();
        var contaB = Guid.NewGuid();
        var registros = new List<RegistroDiario>
        {
            CriarRegistro(contaA, Hoje.AddDays(-1), 1000m),
            CriarRegistro(contaB, Hoje.AddDays(-1), 500m),
        };

        var resultado = _sut.Calcular(registros, new List<ContaRecorrente>(), 5, contaA);

        Assert.Equal(1000m, resultado.SaldoAtual);
    }

    [Fact]
    public void Calcular_ComContaBancariaIdVazio_TrataComoSemFiltro()
    {
        var contaA = Guid.NewGuid();
        var registros = new List<RegistroDiario> { CriarRegistro(contaA, Hoje.AddDays(-1), 1000m) };

        var resultado = _sut.Calcular(registros, new List<ContaRecorrente>(), 3, Guid.Empty);

        Assert.Equal(1000m, resultado.SaldoAtual);
    }

    [Fact]
    public void Calcular_ComProvisionadoFuturo_ApareceNoDiaCorretoEAtualizaSaldo()
    {
        var contaA = Guid.NewGuid();
        var registro = CriarRegistro(contaA, Hoje.AddDays(-1), 1000m);
        registro.ContasReceber.Add(new ContaProvisionada
        {
            Descricao = "Cliente X", Valor = 300m, Categoria = "Vendas", Pago = false, DataVencimento = Hoje.AddDays(3),
        });
        registro.ContasPagar.Add(new ContaProvisionada
        {
            Descricao = "Fornecedor Y", Valor = 100m, Categoria = "Insumos", Pago = false, DataVencimento = Hoje.AddDays(3),
        });

        var resultado = _sut.Calcular(new List<RegistroDiario> { registro }, new List<ContaRecorrente>(), 5, null);

        var diaMovimento = resultado.Dias.Single(d => d.Data == Hoje.AddDays(3));
        Assert.Equal(300m, diaMovimento.TotalEntradas);
        Assert.Equal(100m, diaMovimento.TotalSaidas);
        Assert.Equal("Cliente X", Assert.Single(diaMovimento.Entradas).Descricao);
        Assert.Equal("Provisionado", diaMovimento.Entradas[0].Origem);
        Assert.Equal(1200m, diaMovimento.SaldoFim); // 1000 + 300 - 100
        Assert.False(diaMovimento.SaldoNegativo);
    }

    [Fact]
    public void Calcular_ComProvisionadoQueLevaSaldoNegativo_MarcaSaldoNegativo()
    {
        var contaA = Guid.NewGuid();
        var registro = CriarRegistro(contaA, Hoje.AddDays(-1), 100m);
        registro.ContasPagar.Add(new ContaProvisionada
        {
            Descricao = "Fornecedor", Valor = 500m, Pago = false, DataVencimento = Hoje.AddDays(2),
        });

        var resultado = _sut.Calcular(new List<RegistroDiario> { registro }, new List<ContaRecorrente>(), 5, null);

        var dia = resultado.Dias.Single(d => d.Data == Hoje.AddDays(2));
        Assert.True(dia.SaldoNegativo);
        Assert.Equal(-400m, dia.SaldoFim);
    }

    [Fact]
    public void Calcular_ComRecorrenciaAtiva_ProjetaNosDiasQueOcorre()
    {
        var recorrencia = new ContaRecorrente
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Descricao = "Aluguel", Valor = 200m,
            Tipo = "Pagar", Ativo = true, Periodicidade = "Mensal", DataInicio = Hoje.AddDays(4), CriadoEm = DateTime.UtcNow,
        };

        var resultado = _sut.Calcular(new List<RegistroDiario>(), new List<ContaRecorrente> { recorrencia }, 5, null);

        var dia = resultado.Dias.Single(d => d.Data == Hoje.AddDays(4));
        Assert.Equal(200m, dia.TotalSaidas);
        Assert.Equal("Recorrente", Assert.Single(dia.Saidas).Origem);
    }

    [Fact]
    public void Calcular_ComRecorrenciaJaMaterializadaComoProvisionado_NaoDuplicaLancamento()
    {
        var contaA = Guid.NewGuid();
        var recorrenciaId = Guid.NewGuid();
        var dataOcorrencia = Hoje.AddDays(4);

        var recorrencia = new ContaRecorrente
        {
            Id = recorrenciaId, ClienteId = Guid.NewGuid(), Descricao = "Aluguel", Valor = 200m,
            Tipo = "Pagar", Ativo = true, Periodicidade = "Mensal", DataInicio = dataOcorrencia, CriadoEm = DateTime.UtcNow,
        };

        var registro = CriarRegistro(contaA, Hoje.AddDays(-1), 1000m);
        registro.ContasPagar.Add(new ContaProvisionada
        {
            Descricao = "Aluguel", Valor = 200m, Pago = false, DataVencimento = dataOcorrencia, RecorrenciaId = recorrenciaId,
        });

        var resultado = _sut.Calcular(new List<RegistroDiario> { registro }, new List<ContaRecorrente> { recorrencia }, 5, null);

        var dia = resultado.Dias.Single(d => d.Data == dataOcorrencia);
        // Apenas o provisionado manual deve contar; a recorrência já materializada é ignorada.
        var lancamento = Assert.Single(dia.Saidas);
        Assert.Equal("Provisionado", lancamento.Origem);
        Assert.Equal(200m, dia.TotalSaidas);
    }

    [Fact]
    public void Calcular_RetornaTodosOsDiasMesmoSemMovimento()
    {
        var resultado = _sut.Calcular(new List<RegistroDiario>(), new List<ContaRecorrente>(), 15, null);

        Assert.Equal(15, resultado.Dias.Count);
        for (int d = 1; d <= 15; d++)
            Assert.Contains(resultado.Dias, dia => dia.Data == Hoje.AddDays(d));
    }

    [Fact]
    public void Calcular_RecorrenciaInativa_NaoEntraNaProjecao()
    {
        var recorrencia = new ContaRecorrente
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Descricao = "Assinatura", Valor = 50m,
            Tipo = "Pagar", Ativo = false, Periodicidade = "Mensal", DataInicio = Hoje.AddDays(2), CriadoEm = DateTime.UtcNow,
        };

        var resultado = _sut.Calcular(new List<RegistroDiario>(), new List<ContaRecorrente> { recorrencia }, 5, null);

        Assert.All(resultado.Dias, d => Assert.Equal(0m, d.TotalSaidas));
    }
}
