using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class VerificacaoSinalTransferenciaServiceTests
{
    private readonly Mock<IRegistroRepository> _registroRepoMock = new();
    private readonly Mock<IContaBancariaRepository> _contaRepoMock = new();
    private readonly VerificacaoSinalTransferenciaService _sut;

    public VerificacaoSinalTransferenciaServiceTests()
    {
        _sut = new VerificacaoSinalTransferenciaService(_registroRepoMock.Object, _contaRepoMock.Object);
    }

    [Fact]
    public async Task ListarSinaisSuspeitosAsync_PixRecebidoNaListaDeSaidas_EhSinalizado()
    {
        var clienteId = Guid.NewGuid();
        var contaId = Guid.NewGuid();
        var transferenciaId = Guid.NewGuid();
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = new DateOnly(2026, 6, 4),
            Entradas = new(), ContasReceber = new(), ContasPagar = new(),
            Saidas = new() { new ItemFinanceiroSaida { Descricao = "Pix recebido de Matheus Vidal Lopes", Valor = 3200m, TransferenciaId = transferenciaId } },
        };
        _registroRepoMock.Setup(r => r.ListarTodosNaoExcluidosAsync()).ReturnsAsync(new List<RegistroDiario> { registro });
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(new ContaBancaria { Id = contaId, Nome = "Nubank" });

        var resultado = await _sut.ListarSinaisSuspeitosAsync();

        var item = Assert.Single(resultado);
        Assert.Equal(contaId, item.ContaBancariaId);
        Assert.Equal("Nubank", item.ContaBancariaNome);
        Assert.Equal(3200m, item.Valor);
        Assert.Contains("deveria ser Entrada", item.Motivo);
    }

    [Fact]
    public async Task ListarSinaisSuspeitosAsync_PixEnviadoNaListaDeEntradas_EhSinalizado()
    {
        var clienteId = Guid.NewGuid();
        var contaId = Guid.NewGuid();
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = new DateOnly(2026, 6, 4),
            Saidas = new(), ContasReceber = new(), ContasPagar = new(),
            Entradas = new() { new ItemFinanceiro { Descricao = "Transferência enviada pelo Pix", Valor = 400m, TransferenciaId = Guid.NewGuid() } },
        };
        _registroRepoMock.Setup(r => r.ListarTodosNaoExcluidosAsync()).ReturnsAsync(new List<RegistroDiario> { registro });
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(contaId)).ReturnsAsync(new ContaBancaria { Id = contaId, Nome = "C6 Bank" });

        var resultado = await _sut.ListarSinaisSuspeitosAsync();

        var item = Assert.Single(resultado);
        Assert.Contains("deveria ser Saída", item.Motivo);
    }

    [Fact]
    public async Task ListarSinaisSuspeitosAsync_SemTransferenciaId_NuncaEhSinalizado()
    {
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), ContaBancariaId = Guid.NewGuid(), Data = new DateOnly(2026, 6, 4),
            Entradas = new(), ContasReceber = new(), ContasPagar = new(),
            // Mesma palavra-chave, mas nunca foi classificado como transferência — não é o bug.
            Saidas = new() { new ItemFinanceiroSaida { Descricao = "Pix recebido de alguém", Valor = 100m, TransferenciaId = null } },
        };
        _registroRepoMock.Setup(r => r.ListarTodosNaoExcluidosAsync()).ReturnsAsync(new List<RegistroDiario> { registro });

        var resultado = await _sut.ListarSinaisSuspeitosAsync();

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task ListarSinaisSuspeitosAsync_PixCorretamenteClassificado_NuncaEhSinalizado()
    {
        var contaId = Guid.NewGuid();
        var registro = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), ContaBancariaId = contaId, Data = new DateOnly(2026, 6, 4),
            Saidas = new(), ContasReceber = new(), ContasPagar = new(),
            Entradas = new() { new ItemFinanceiro { Descricao = "Pix recebido de Matheus Vidal Lopes", Valor = 3200m, TransferenciaId = Guid.NewGuid() } },
        };
        _registroRepoMock.Setup(r => r.ListarTodosNaoExcluidosAsync()).ReturnsAsync(new List<RegistroDiario> { registro });

        var resultado = await _sut.ListarSinaisSuspeitosAsync();

        Assert.Empty(resultado);
    }
}
