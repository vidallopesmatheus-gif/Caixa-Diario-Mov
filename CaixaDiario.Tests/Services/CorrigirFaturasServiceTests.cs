using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class CorrigirFaturasServiceTests
{
    private readonly Mock<IContaBancariaRepository> _contaRepoMock = new();
    private readonly Mock<IRegistroRepository> _registroRepoMock = new();
    private readonly Mock<IFaturaRepository> _faturaRepoMock = new();
    private readonly Mock<IPagamentoFaturaRepository> _pagamentoRepoMock = new();
    private readonly Mock<IFaturaCartaoService> _faturaCartaoServiceMock = new();
    private readonly CorrigirFaturasService _sut;

    public CorrigirFaturasServiceTests()
    {
        _sut = new CorrigirFaturasService(
            _contaRepoMock.Object, _registroRepoMock.Object, _faturaRepoMock.Object,
            _pagamentoRepoMock.Object, _faturaCartaoServiceMock.Object);
    }

    private static ContaBancaria CriarCartao() => new()
    {
        Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Nome = "Cartões", Tipo = "CartaoCredito",
        SaldoInicial = 0m, Ativa = true, DataCriacao = DateTime.UtcNow,
    };

    [Fact]
    public async Task CorrigirAsync_SemConfirmar_SoDetectaAsDuplicadasNaoGravaNada()
    {
        var conta = CriarCartao();
        var fatura1 = new Fatura { Id = Guid.NewGuid(), ClienteId = conta.ClienteId, ContaBancariaId = conta.Id, Banco = "C6", Competencia = "2026-10", DataVencimento = new DateOnly(2026, 10, 20), CriadoEm = new DateTime(2026, 10, 1) };
        var fatura2 = new Fatura { Id = Guid.NewGuid(), ClienteId = conta.ClienteId, ContaBancariaId = conta.Id, Banco = "c6", Competencia = "2026-10", DataVencimento = new DateOnly(2026, 10, 20), CriadoEm = new DateTime(2026, 10, 5) };
        _contaRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<ContaBancaria> { conta });
        _faturaRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<Fatura> { fatura1, fatura2 });

        var resultado = await _sut.CorrigirAsync(confirmar: false);

        Assert.False(resultado.Confirmado);
        Assert.Equal(1, resultado.FaturasMescladas);
        var mescla = Assert.Single(resultado.Mesclagens);
        Assert.Equal(fatura1.Id, mescla.FaturaMantidaId); // a mais antiga
        Assert.Equal(fatura2.Id, mescla.FaturaRemovidaId);
        Assert.Equal(0, resultado.PagamentosVinculados);
        _faturaRepoMock.Verify(r => r.RemoverAsync(It.IsAny<Fatura>()), Times.Never);
        _faturaCartaoServiceMock.Verify(s => s.AutoVincularPagamentoPorTransferenciaAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task CorrigirAsync_ComConfirmar_RepontaComprasEPagamentosERemoveAFaturaDuplicada()
    {
        var conta = CriarCartao();
        var fatura1 = new Fatura { Id = Guid.NewGuid(), ClienteId = conta.ClienteId, ContaBancariaId = conta.Id, Banco = "C6", Competencia = "2026-10", DataVencimento = new DateOnly(2026, 10, 20), CriadoEm = new DateTime(2026, 10, 1) };
        var fatura2 = new Fatura { Id = Guid.NewGuid(), ClienteId = conta.ClienteId, ContaBancariaId = conta.Id, Banco = "C6", Competencia = "2026-10", DataVencimento = new DateOnly(2026, 10, 20), CriadoEm = new DateTime(2026, 10, 5) };
        var itemDuplicada = new ItemFinanceiroSaida { Id = Guid.NewGuid(), Descricao = "Compra", Valor = 50m, FaturaId = fatura2.Id };
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = conta.ClienteId, ContaBancariaId = conta.Id, Data = new DateOnly(2026, 9, 26),
            Entradas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = -50m,
            Saidas = new() { itemDuplicada },
        };
        var pagamentoDuplicada = new PagamentoFatura { Id = Guid.NewGuid(), ClienteId = conta.ClienteId, ContaCartaoId = conta.Id, ContaOrigemId = Guid.NewGuid(), Competencia = "2026-10", FaturaId = fatura2.Id, ValorPago = 50m, Data = new DateOnly(2026, 10, 20), CriadoEm = DateTime.UtcNow };

        _contaRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<ContaBancaria> { conta });
        _faturaRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<Fatura> { fatura1, fatura2 });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<RegistroDiario> { registro });
        _pagamentoRepoMock.Setup(r => r.ListarPorContaCartaoAsync(conta.Id)).ReturnsAsync(new List<PagamentoFatura> { pagamentoDuplicada });
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);
        _pagamentoRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<PagamentoFatura>())).ReturnsAsync((PagamentoFatura p) => p);
        _faturaCartaoServiceMock.Setup(s => s.AutoVincularPagamentoPorTransferenciaAsync(conta.Id, fatura1.Id)).ReturnsAsync(true);

        var resultado = await _sut.CorrigirAsync(confirmar: true);

        Assert.True(resultado.Confirmado);
        Assert.Equal(1, resultado.FaturasMescladas);
        Assert.Equal(1, resultado.PagamentosVinculados);

        Assert.Equal(fatura1.Id, itemDuplicada.FaturaId); // repontado
        Assert.Equal(fatura1.Id, pagamentoDuplicada.FaturaId); // repontado
        _faturaRepoMock.Verify(r => r.RemoverAsync(fatura2), Times.Once);
    }

    [Fact]
    public async Task CorrigirAsync_SemDuplicatas_NadaAFazer()
    {
        var conta = CriarCartao();
        var fatura = new Fatura { Id = Guid.NewGuid(), ClienteId = conta.ClienteId, ContaBancariaId = conta.Id, Banco = "Nubank", Competencia = "2026-09", DataVencimento = new DateOnly(2026, 9, 10), CriadoEm = DateTime.UtcNow };
        _contaRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<ContaBancaria> { conta });
        _faturaRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<Fatura> { fatura });

        var resultado = await _sut.CorrigirAsync(confirmar: false);

        Assert.Equal(0, resultado.FaturasMescladas);
        Assert.Empty(resultado.Mesclagens);
    }
}
