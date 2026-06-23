using CaixaDiario.API.DTOs.ContasRecorrentes;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class ContaRecorrenteServiceTests
{
    private readonly Mock<IContaRecorrenteRepository> _repoMock = new();
    private readonly Mock<IAuditService> _auditMock = new();
    private ContaRecorrenteService CriarSut() => new(_repoMock.Object, _auditMock.Object);

    private static ContaRecorrente CriarConta(Guid clienteId) => new()
    {
        Id = Guid.NewGuid(), ClienteId = clienteId, Descricao = "Aluguel",
        Valor = 1000m, Tipo = "Pagar", DataInicio = new DateOnly(2026, 1, 1),
        Ativo = true, CriadoEm = DateTime.UtcNow,
    };

    [Fact]
    public async Task Listar_ClienteAcessandoOutroCliente_LancaAcessoNegado()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            CriarSut().ListarPorClienteAsync(Guid.NewGuid(), Guid.NewGuid(), "cliente"));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Listar_Admin_RetornaLista()
    {
        var clienteId = Guid.NewGuid();
        _repoMock.Setup(r => r.ListarAtivasPorClienteAsync(clienteId))
            .ReturnsAsync(new List<ContaRecorrente> { CriarConta(clienteId) });
        var resultado = await CriarSut().ListarPorClienteAsync(clienteId, Guid.NewGuid(), "admin");
        Assert.Single(resultado);
    }

    [Fact]
    public async Task Criar_TipoInvalido_LancaDadosInvalidos()
    {
        var dto = new CriarContaRecorrenteDto
        {
            ClienteId = Guid.NewGuid(), Descricao = "Teste", Valor = 100m,
            Tipo = "Invalido", DataInicio = new DateOnly(2026, 1, 1),
        };
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            CriarSut().CriarAsync(dto, dto.ClienteId, "cliente"));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Criar_Valido_RetornaDto()
    {
        var clienteId = Guid.NewGuid();
        var dto = new CriarContaRecorrenteDto
        {
            ClienteId = clienteId, Descricao = "Aluguel", Valor = 1000m,
            Tipo = "Pagar", DataInicio = new DateOnly(2026, 1, 1),
        };
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<ContaRecorrente>()))
            .ReturnsAsync((ContaRecorrente c) => c);
        _auditMock.Setup(a => a.LogAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        var resultado = await CriarSut().CriarAsync(dto, clienteId, "cliente");
        Assert.Equal("Aluguel", resultado.Descricao);
        Assert.True(resultado.Ativo);
    }

    [Fact]
    public async Task Desativar_NaoEncontrada_LancaNaoEncontrada()
    {
        var clienteId = Guid.NewGuid();
        _repoMock.Setup(r => r.ObterPorIdAsync(clienteId, It.IsAny<Guid>()))
            .ReturnsAsync((ContaRecorrente?)null);
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            CriarSut().DesativarAsync(clienteId, Guid.NewGuid(), clienteId, "cliente"));
        Assert.Equal(404, ex.StatusCode);
        Assert.Equal(CodigoRetorno.CONTA_RECORRENTE_NAO_ENCONTRADA, ex.Codigo);
    }

    [Fact]
    public async Task Desativar_ClienteAcessandoOutroCliente_LancaAcessoNegado()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            CriarSut().DesativarAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "cliente"));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Desativar_Valido_MarcaInativaERegistraAuditoria()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId);
        _repoMock.Setup(r => r.ObterPorIdAsync(clienteId, conta.Id)).ReturnsAsync(conta);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<ContaRecorrente>()))
            .ReturnsAsync((ContaRecorrente c) => c);
        _auditMock.Setup(a => a.LogAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        await CriarSut().DesativarAsync(clienteId, conta.Id, clienteId, "cliente");

        Assert.False(conta.Ativo);
        _repoMock.Verify(r => r.AtualizarAsync(It.Is<ContaRecorrente>(c => !c.Ativo)), Times.Once);
        _auditMock.Verify(a => a.LogAsync(clienteId, clienteId, "ContaRecorrente", "Exclusao",
            conta.Id.ToString(), It.IsAny<string?>(), null), Times.Once);
    }

    [Fact]
    public async Task Atualizar_ClienteAcessandoOutroCliente_LancaAcessoNegado()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            CriarSut().AtualizarAsync(Guid.NewGuid(), Guid.NewGuid(),
                new AtualizarContaRecorrenteDto(), Guid.NewGuid(), "cliente"));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Atualizar_NaoEncontrada_LancaNaoEncontrada()
    {
        var clienteId = Guid.NewGuid();
        _repoMock.Setup(r => r.ObterPorIdAsync(clienteId, It.IsAny<Guid>()))
            .ReturnsAsync((ContaRecorrente?)null);
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            CriarSut().AtualizarAsync(clienteId, Guid.NewGuid(),
                new AtualizarContaRecorrenteDto(), clienteId, "cliente"));
        Assert.Equal(404, ex.StatusCode);
        Assert.Equal(CodigoRetorno.CONTA_RECORRENTE_NAO_ENCONTRADA, ex.Codigo);
    }

    [Fact]
    public async Task Atualizar_TodosOsCampos_AplicaAlteracoesERegistraAuditoria()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId);
        _repoMock.Setup(r => r.ObterPorIdAsync(clienteId, conta.Id)).ReturnsAsync(conta);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<ContaRecorrente>()))
            .ReturnsAsync((ContaRecorrente c) => c);
        _auditMock.Setup(a => a.LogAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var dto = new AtualizarContaRecorrenteDto
        {
            Descricao = "Aluguel novo",
            Valor = 1500m,
            Categoria = "Despesas Administrativas",
            DataFim = new DateOnly(2026, 12, 31),
        };

        var resultado = await CriarSut().AtualizarAsync(clienteId, conta.Id, dto, clienteId, "cliente");

        Assert.Equal("Aluguel novo", resultado.Descricao);
        Assert.Equal(1500m, resultado.Valor);
        Assert.Equal("Despesas Administrativas", resultado.Categoria);
        Assert.Equal(new DateOnly(2026, 12, 31), resultado.DataFim);
        _auditMock.Verify(a => a.LogAsync(clienteId, clienteId, "ContaRecorrente", "Edicao",
            conta.Id.ToString(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task Atualizar_SemCampos_MantemValoresEAtualiza()
    {
        var clienteId = Guid.NewGuid();
        var conta = CriarConta(clienteId);
        _repoMock.Setup(r => r.ObterPorIdAsync(clienteId, conta.Id)).ReturnsAsync(conta);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<ContaRecorrente>()))
            .ReturnsAsync((ContaRecorrente c) => c);
        _auditMock.Setup(a => a.LogAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var resultado = await CriarSut().AtualizarAsync(clienteId, conta.Id,
            new AtualizarContaRecorrenteDto(), clienteId, "admin");

        Assert.Equal("Aluguel", resultado.Descricao);
        Assert.Equal(1000m, resultado.Valor);
        _repoMock.Verify(r => r.AtualizarAsync(It.IsAny<ContaRecorrente>()), Times.Once);
    }
}
