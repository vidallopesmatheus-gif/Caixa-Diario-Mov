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
}
