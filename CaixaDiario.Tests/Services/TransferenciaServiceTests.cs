using CaixaDiario.API.DTOs.Transferencias;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class TransferenciaServiceTests
{
    private readonly Mock<ITransferenciaRepository> _transferenciaRepoMock = new();
    private readonly Mock<IContaBancariaRepository> _contaRepoMock = new();
    private readonly Mock<IRegistroRepository> _registroRepoMock = new();
    private readonly Mock<IAuditService> _auditMock = new();
    private readonly TransferenciaService _sut;

    public TransferenciaServiceTests()
    {
        _sut = new TransferenciaService(_transferenciaRepoMock.Object, _contaRepoMock.Object, _registroRepoMock.Object, _auditMock.Object);
    }

    private static ContaBancaria CriarConta(Guid clienteId, string nome, string tipo = "Caixa", decimal saldoInicial = 0m, bool ativa = true) => new()
    {
        Id = Guid.NewGuid(), ClienteId = clienteId, Nome = nome, Tipo = tipo, SaldoInicial = saldoInicial, Ativa = ativa, DataCriacao = DateTime.UtcNow,
    };

    [Fact]
    public async Task CriarAsync_ContasValidasSemRegistroNoDia_CriaParDeLancamentosERecalculaSaldos()
    {
        var clienteId = Guid.NewGuid();
        var origem = CriarConta(clienteId, "Caixa", saldoInicial: 1000m);
        var destino = CriarConta(clienteId, "CDI Nubank", tipo: "Investimento", saldoInicial: 0m);
        var data = new DateOnly(2026, 8, 1);

        _contaRepoMock.Setup(r => r.ObterPorIdAsync(origem.Id)).ReturnsAsync(origem);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(destino.Id)).ReturnsAsync(destino);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(It.IsAny<Guid>(), data)).ReturnsAsync((RegistroDiario?)null);
        _registroRepoMock.Setup(r => r.ListarPorContaAsync(It.IsAny<Guid>())).ReturnsAsync(new List<RegistroDiario>());

        RegistroDiario? registroOrigemAdicionado = null;
        RegistroDiario? registroDestinoAdicionado = null;
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.Is<RegistroDiario>(x => x.ContaBancariaId == origem.Id)))
            .Callback<RegistroDiario>(r => registroOrigemAdicionado = r).ReturnsAsync((RegistroDiario r) => r);
        _registroRepoMock.Setup(r => r.AdicionarAsync(It.Is<RegistroDiario>(x => x.ContaBancariaId == destino.Id)))
            .Callback<RegistroDiario>(r => registroDestinoAdicionado = r).ReturnsAsync((RegistroDiario r) => r);
        _transferenciaRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<Transferencia>())).ReturnsAsync((Transferencia t) => t);

        var dto = new CriarTransferenciaDto { ClienteId = clienteId, ContaOrigemId = origem.Id, ContaDestinoId = destino.Id, Data = data, Valor = 300m };
        var resultado = await _sut.CriarAsync(dto, clienteId, "cliente");

        Assert.Equal(300m, resultado.Valor);
        Assert.NotNull(registroOrigemAdicionado);
        Assert.NotNull(registroDestinoAdicionado);

        var saidaOrigem = Assert.Single(registroOrigemAdicionado!.Saidas);
        Assert.Equal("Transferencia", saidaOrigem.TipoCusto);
        Assert.Equal(300m, saidaOrigem.Valor);
        Assert.Equal(700m, registroOrigemAdicionado.SaldoFinal); // 1000 (Inicio = SaldoInicial) - 300

        var entradaDestino = Assert.Single(registroDestinoAdicionado!.Entradas);
        Assert.Equal("Transferencia", entradaDestino.TipoCusto);
        Assert.Equal(300m, entradaDestino.Valor);
        Assert.Equal(300m, registroDestinoAdicionado.SaldoFinal); // 0 + 300
        Assert.Equal(saidaOrigem.TransferenciaId, entradaDestino.TransferenciaId);

        _transferenciaRepoMock.Verify(r => r.AdicionarAsync(It.Is<Transferencia>(t =>
            t.ContaOrigemId == origem.Id && t.ContaDestinoId == destino.Id && t.Valor == 300m)), Times.Once);
    }

    [Fact]
    public async Task CriarAsync_ContaOrigemIgualContaDestino_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var contaId = Guid.NewGuid();
        var dto = new CriarTransferenciaDto { ClienteId = clienteId, ContaOrigemId = contaId, ContaDestinoId = contaId, Data = new DateOnly(2026, 8, 1), Valor = 10m };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.CriarAsync(dto, clienteId, "cliente"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    [Fact]
    public async Task CriarAsync_ContaInativa_LancaContaInativa()
    {
        var clienteId = Guid.NewGuid();
        var origem = CriarConta(clienteId, "Caixa", ativa: false);
        var destino = CriarConta(clienteId, "Investimentos", tipo: "Investimento");
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(origem.Id)).ReturnsAsync(origem);
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(destino.Id)).ReturnsAsync(destino);

        var dto = new CriarTransferenciaDto { ClienteId = clienteId, ContaOrigemId = origem.Id, ContaDestinoId = destino.Id, Data = new DateOnly(2026, 8, 1), Valor = 10m };
        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.CriarAsync(dto, clienteId, "cliente"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.CONTA_INATIVA, ex.Codigo);
    }

    [Fact]
    public async Task CriarAsync_ClienteAcessandoOutroCliente_LancaAcessoNegado()
    {
        var dto = new CriarTransferenciaDto { ClienteId = Guid.NewGuid(), ContaOrigemId = Guid.NewGuid(), ContaDestinoId = Guid.NewGuid(), Data = new DateOnly(2026, 8, 1), Valor = 10m };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.CriarAsync(dto, Guid.NewGuid(), "cliente"));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task EstornarAsync_RemovePartesDosDoisRegistrosERevertaSaldo()
    {
        var clienteId = Guid.NewGuid();
        var transferenciaId = Guid.NewGuid();
        var origemId = Guid.NewGuid();
        var destinoId = Guid.NewGuid();
        var data = new DateOnly(2026, 8, 1);

        var transferencia = new Transferencia
        {
            Id = transferenciaId, ClienteId = clienteId, ContaOrigemId = origemId, ContaDestinoId = destinoId,
            Data = data, Valor = 300m, CriadoEm = DateTime.UtcNow,
        };

        var regOrigem = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = origemId, Data = data, Inicio = 1000m,
            Entradas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 700m,
            Saidas = new() { new ItemFinanceiroSaida { Descricao = "x", Valor = 300m, Categoria = "Transferência", TipoCusto = "Transferencia", TransferenciaId = transferenciaId } },
        };
        var regDestino = new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = destinoId, Data = data, Inicio = 0m,
            Saidas = new(), ContasReceber = new(), ContasPagar = new(), SaldoFinal = 300m,
            Entradas = new() { new ItemFinanceiro { Descricao = "x", Valor = 300m, Categoria = "Transferência", TipoCusto = "Transferencia", TransferenciaId = transferenciaId } },
        };

        _transferenciaRepoMock.Setup(r => r.ObterPorIdAsync(transferenciaId)).ReturnsAsync(transferencia);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(origemId, data)).ReturnsAsync(regOrigem);
        _registroRepoMock.Setup(r => r.ObterPorContaEDataAsync(destinoId, data)).ReturnsAsync(regDestino);

        await _sut.EstornarAsync(transferenciaId, clienteId, "cliente");

        Assert.Empty(regOrigem.Saidas);
        Assert.Equal(1000m, regOrigem.SaldoFinal);
        Assert.Empty(regDestino.Entradas);
        Assert.Equal(0m, regDestino.SaldoFinal);
        _transferenciaRepoMock.Verify(r => r.RemoverAsync(transferencia), Times.Once);
    }

    [Fact]
    public async Task EstornarAsync_TransferenciaInexistente_LancaNaoEncontrada()
    {
        _transferenciaRepoMock.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>())).ReturnsAsync((Transferencia?)null);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.EstornarAsync(Guid.NewGuid(), Guid.NewGuid(), "admin"));

        Assert.Equal(404, ex.StatusCode);
        Assert.Equal(CodigoRetorno.TRANSFERENCIA_NAO_ENCONTRADA, ex.Codigo);
    }
}
