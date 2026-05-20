using CaixaDiario.API.DTOs.Metas;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class MetaServiceTests
{
    private readonly Mock<IMetaRepository> _repoMock = new();
    private readonly MetaService _sut;

    public MetaServiceTests() => _sut = new MetaService(_repoMock.Object);

    [Fact]
    public async Task Obter_ClienteAcessandoOutroCliente_LancaAcessoNegado()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ObterMetaAsync(Guid.NewGuid(), 2026, Guid.NewGuid(), "cliente"));
        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.ACESSO_NEGADO, ex.Codigo);
    }

    [Fact]
    public async Task Obter_MetaNaoEncontrada_LancaMetaNaoEncontrada()
    {
        var clienteId = Guid.NewGuid();
        _repoMock.Setup(r => r.ObterPorClienteEAnoAsync(clienteId, 2026)).ReturnsAsync((MetaAnual?)null);
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ObterMetaAsync(clienteId, 2026, clienteId, "cliente"));
        Assert.Equal(404, ex.StatusCode);
        Assert.Equal(CodigoRetorno.META_NAO_ENCONTRADA, ex.Codigo);
    }

    [Fact]
    public async Task Obter_Admin_RetornaMeta()
    {
        var clienteId = Guid.NewGuid();
        var meta = new MetaAnual { Id = Guid.NewGuid(), ClienteId = clienteId, Ano = 2026, MetaReceita = 120000m, MetaLucro = 60000m, CriadoEm = DateTime.UtcNow, AtualizadoEm = DateTime.UtcNow };
        _repoMock.Setup(r => r.ObterPorClienteEAnoAsync(clienteId, 2026)).ReturnsAsync(meta);
        var resultado = await _sut.ObterMetaAsync(clienteId, 2026, Guid.NewGuid(), "admin");
        Assert.Equal(120000m, resultado.MetaReceita);
    }

    [Fact]
    public async Task Salvar_ClienteAcessandoOutroCliente_LancaAcessoNegado()
    {
        var dto = new SalvarMetaAnualDto { ClienteId = Guid.NewGuid(), Ano = 2026, MetaReceita = 100000m, MetaLucro = 50000m };
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.SalvarMetaAsync(dto, Guid.NewGuid(), "cliente"));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Salvar_Nova_RetornaDto()
    {
        var clienteId = Guid.NewGuid();
        var dto = new SalvarMetaAnualDto { ClienteId = clienteId, Ano = 2026, MetaReceita = 120000m, MetaLucro = 60000m };
        _repoMock.Setup(r => r.ObterPorClienteEAnoAsync(clienteId, 2026)).ReturnsAsync((MetaAnual?)null);
        _repoMock.Setup(r => r.SalvarAsync(It.IsAny<MetaAnual>())).ReturnsAsync((MetaAnual m) => m);
        var resultado = await _sut.SalvarMetaAsync(dto, clienteId, "cliente");
        Assert.Equal(120000m, resultado.MetaReceita);
        Assert.Equal(2026, resultado.Ano);
    }

    [Fact]
    public async Task Salvar_Existente_AtualizaERetorna()
    {
        var clienteId = Guid.NewGuid();
        var dto = new SalvarMetaAnualDto { ClienteId = clienteId, Ano = 2026, MetaReceita = 200000m, MetaLucro = 100000m };
        var existente = new MetaAnual { Id = Guid.NewGuid(), ClienteId = clienteId, Ano = 2026, MetaReceita = 100000m, MetaLucro = 50000m, CriadoEm = DateTime.UtcNow, AtualizadoEm = DateTime.UtcNow };
        _repoMock.Setup(r => r.ObterPorClienteEAnoAsync(clienteId, 2026)).ReturnsAsync(existente);
        _repoMock.Setup(r => r.SalvarAsync(It.IsAny<MetaAnual>())).ReturnsAsync((MetaAnual m) => m);
        var resultado = await _sut.SalvarMetaAsync(dto, clienteId, "cliente");
        Assert.Equal(200000m, resultado.MetaReceita);
    }
}
