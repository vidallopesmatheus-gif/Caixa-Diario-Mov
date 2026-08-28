using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class MetaProgressoServiceTests
{
    private readonly Mock<IContaBancariaRepository> _contaRepoMock = new();
    private readonly Mock<IRegistroRepository> _registroRepoMock = new();
    private readonly MetaProgressoService _sut;

    public MetaProgressoServiceTests()
    {
        _sut = new MetaProgressoService(_contaRepoMock.Object, _registroRepoMock.Object);
    }

    private static ContaBancaria CriarConta(Guid clienteId, decimal saldoInicial = 0m) => new()
    {
        Id = Guid.NewGuid(), ClienteId = clienteId, Nome = "CDI Nubank", Tipo = "Investimento",
        SaldoInicial = saldoInicial, Ativa = true, DataCriacao = DateTime.UtcNow,
    };

    [Fact]
    public async Task AplicarSaldoDeContasVinculadas_UmaMetaSemVinculo_NaoAlteraTotalInvestido()
    {
        var clienteId = Guid.NewGuid();
        var meta = new MetaAnual { Id = Guid.NewGuid(), ClienteId = clienteId, Ano = 2026, ValorSonho = 10000m, TotalInvestido = 4000m };

        await _sut.AplicarSaldoDeContasVinculadasAsync(clienteId, new List<MetaAnual> { meta });

        Assert.Equal(4000m, meta.TotalInvestido);
        _contaRepoMock.Verify(r => r.ListarPorClienteAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task AplicarSaldoDeContasVinculadas_UmaContaUmaMeta_DerivaTotalInvestidoDoSaldo()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId);
        var meta = new MetaAnual { Id = Guid.NewGuid(), ClienteId = clienteId, Ano = 2026, ValorSonho = 2000m, TotalInvestido = 999m, ContaInvestimentoId = conta.Id };

        _contaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria> { conta });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario>
        {
            new() { Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = conta.Id, Data = new DateOnly(2026, 8, 1), SaldoFinal = 1050m, Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new() },
        });

        await _sut.AplicarSaldoDeContasVinculadasAsync(clienteId, new List<MetaAnual> { meta });

        // saldo 1050 / meta 2000 = 52,5%
        Assert.Equal(1050m, meta.TotalInvestido);
    }

    [Fact]
    public async Task AplicarSaldoDeContasVinculadas_DuasMetasMesmaConta_MostramPercentualCombinadoIgual()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId);
        var metaA = new MetaAnual { Id = Guid.NewGuid(), ClienteId = clienteId, Ano = 2026, ValorSonho = 2000m, ContaInvestimentoId = conta.Id };
        var metaB = new MetaAnual { Id = Guid.NewGuid(), ClienteId = clienteId, Ano = 2027, ValorSonho = 1000m, ContaInvestimentoId = conta.Id };

        _contaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria> { conta });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario>
        {
            new() { Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = conta.Id, Data = new DateOnly(2026, 8, 1), SaldoFinal = 1050m, Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new() },
        });

        await _sut.AplicarSaldoDeContasVinculadasAsync(clienteId, new List<MetaAnual> { metaA, metaB });

        // saldo 1050 / soma(2000+1000=3000) = 35% aplicado às duas
        Assert.Equal(700m, metaA.TotalInvestido);  // 2000 * 0.35
        Assert.Equal(350m, metaB.TotalInvestido);  // 1000 * 0.35
    }

    [Fact]
    public async Task AplicarSaldoDeContasVinculadas_SaldoMaiorQueSomaDasMetas_CapaEm100Porcento()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId);
        var meta = new MetaAnual { Id = Guid.NewGuid(), ClienteId = clienteId, Ano = 2026, ValorSonho = 500m, ContaInvestimentoId = conta.Id };

        _contaRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaBancaria> { conta });
        _registroRepoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario>
        {
            new() { Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = conta.Id, Data = new DateOnly(2026, 8, 1), SaldoFinal = 5000m, Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new() },
        });

        await _sut.AplicarSaldoDeContasVinculadasAsync(clienteId, new List<MetaAnual> { meta });

        Assert.Equal(500m, meta.TotalInvestido); // capado no valor da própria meta (100%)
    }
}
