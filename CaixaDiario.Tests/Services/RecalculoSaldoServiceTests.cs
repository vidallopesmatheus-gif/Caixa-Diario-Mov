using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class RecalculoSaldoServiceTests
{
    private readonly Mock<IContaBancariaRepository> _contaRepoMock = new();
    private readonly Mock<IRegistroRepository> _registroRepoMock = new();
    private readonly RecalculoSaldoService _sut;

    public RecalculoSaldoServiceTests()
    {
        _sut = new RecalculoSaldoService(_contaRepoMock.Object, _registroRepoMock.Object);
    }

    private static ContaBancaria CriarConta(decimal saldoInicial = 0m) => new()
    {
        Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Nome = "Nubank", Tipo = "ContaCorrente",
        SaldoInicial = saldoInicial, Ativa = true, DataCriacao = DateTime.UtcNow,
    };

    private static RegistroDiario CriarRegistro(Guid contaId, Guid clienteId, DateOnly data, decimal inicio, decimal saldoFinal) => new()
    {
        Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = data,
        Inicio = inicio, SaldoFinal = saldoFinal, Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(),
    };

    [Fact]
    public async Task RecalcularTodasAsContasAsync_SemConfirmar_SoRelatorioNaoGravaNada()
    {
        var conta = CriarConta(saldoInicial: 1000m);
        // 18/10 fecha em 816,72 mas 19/10 abre arrastando um Inicio velho de 132,66 — exatamente o
        // bug real relatado (Nubank).
        var dia1 = CriarRegistro(conta.Id, conta.ClienteId, new DateOnly(2026, 10, 18), 1000m, 816.72m);
        var dia2 = CriarRegistro(conta.Id, conta.ClienteId, new DateOnly(2026, 10, 19), 132.66m, -575.12m);
        _contaRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<ContaBancaria> { conta });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<RegistroDiario> { dia1, dia2 });

        var resultado = await _sut.RecalcularTodasAsContasAsync(confirmar: false);

        Assert.False(resultado.Confirmado);
        Assert.Equal(1, resultado.ContasComDivergencia);
        var item = Assert.Single(resultado.Itens);
        Assert.Equal(conta.Id, item.ContaBancariaId);
        Assert.Equal(new DateOnly(2026, 10, 19), item.PrimeiraDataDivergente);
        Assert.Equal(-575.12m, item.SaldoAtualAntes);
        // -575,12 (dia2 antes da cascata) tinha delta local de -707,78 (−575,12 − 132,66); com o
        // Inicio corrigido (816,72), o saldo final correto é 816,72 − 707,78 = 108,94.
        Assert.Equal(108.94m, item.SaldoAtualDepois);

        // Nada foi gravado.
        Assert.Equal(132.66m, dia2.Inicio);
        Assert.Equal(-575.12m, dia2.SaldoFinal);
        _registroRepoMock.Verify(r => r.AtualizarAsync(It.IsAny<RegistroDiario>()), Times.Never);
    }

    [Fact]
    public async Task RecalcularTodasAsContasAsync_ComConfirmar_CorrigeACadeiaDeVerdade()
    {
        var conta = CriarConta(saldoInicial: 1000m);
        var dia1 = CriarRegistro(conta.Id, conta.ClienteId, new DateOnly(2026, 10, 18), 1000m, 816.72m);
        var dia2 = CriarRegistro(conta.Id, conta.ClienteId, new DateOnly(2026, 10, 19), 132.66m, -575.12m);
        _contaRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<ContaBancaria> { conta });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<RegistroDiario> { dia1, dia2 });
        _registroRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<RegistroDiario>())).ReturnsAsync((RegistroDiario r) => r);

        var resultado = await _sut.RecalcularTodasAsContasAsync(confirmar: true);

        Assert.True(resultado.Confirmado);
        Assert.Equal(816.72m, dia2.Inicio);
        Assert.Equal(108.94m, dia2.SaldoFinal);
        // dia1 já estava consistente (Inicio == SaldoInicial da conta) — não precisa gravar.
        _registroRepoMock.Verify(r => r.AtualizarAsync(dia1), Times.Never);
        _registroRepoMock.Verify(r => r.AtualizarAsync(dia2), Times.Once);
    }

    [Fact]
    public async Task RecalcularTodasAsContasAsync_ContaSemDivergencia_NaoEntraNoRelatorio()
    {
        var conta = CriarConta(saldoInicial: 500m);
        var dia1 = CriarRegistro(conta.Id, conta.ClienteId, new DateOnly(2026, 10, 18), 500m, 700m);
        _contaRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<ContaBancaria> { conta });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<RegistroDiario> { dia1 });

        var resultado = await _sut.RecalcularTodasAsContasAsync(confirmar: false);

        Assert.Equal(0, resultado.ContasComDivergencia);
        Assert.Empty(resultado.Itens);
    }

    [Fact]
    public async Task RecalcularTodasAsContasAsync_ContaSemRegistros_EIgnorada()
    {
        var conta = CriarConta();
        _contaRepoMock.Setup(r => r.ListarTodasAsync()).ReturnsAsync(new List<ContaBancaria> { conta });
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(conta.Id)).ReturnsAsync(new List<RegistroDiario>());

        var resultado = await _sut.RecalcularTodasAsContasAsync(confirmar: false);

        Assert.Empty(resultado.Itens);
    }
}
