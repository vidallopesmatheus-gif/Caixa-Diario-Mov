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
    private readonly Mock<IMetaProgressoService> _metaProgressoMock = new();
    private readonly MetaService _sut;

    public MetaServiceTests() => _sut = new MetaService(_repoMock.Object, _metaProgressoMock.Object);

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
        _repoMock.Setup(r => r.ObterMetaSimplesPorClienteEAnoAsync(clienteId, 2026)).ReturnsAsync((MetaAnual?)null);
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
        _repoMock.Setup(r => r.ObterMetaSimplesPorClienteEAnoAsync(clienteId, 2026)).ReturnsAsync(meta);
        var resultado = await _sut.ObterMetaAsync(clienteId, 2026, Guid.NewGuid(), "admin");
        Assert.Equal(120000m, resultado.MetaReceita);
    }

    [Fact]
    public async Task ListarMetas_ClienteProprio_RetornaOrdenadoPorAnoDecrescente()
    {
        var clienteId = Guid.NewGuid();
        var metas = new List<MetaAnual>
        {
            new() { Id = Guid.NewGuid(), ClienteId = clienteId, Ano = 2025, MetaReceita = 1m, MetaLucro = 1m, CriadoEm = DateTime.UtcNow, AtualizadoEm = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), ClienteId = clienteId, Ano = 2027, MetaReceita = 1m, MetaLucro = 1m, CriadoEm = DateTime.UtcNow, AtualizadoEm = DateTime.UtcNow },
        };
        _repoMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(metas);

        var resultado = await _sut.ListarMetasAsync(clienteId, clienteId, "cliente");

        Assert.Equal(2027, resultado[0].Ano);
        Assert.Equal(2025, resultado[1].Ano);
    }

    [Fact]
    public async Task ListarMetas_ClienteAcessandoOutroCliente_LancaAcessoNegado()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.ListarMetasAsync(Guid.NewGuid(), Guid.NewGuid(), "cliente"));
        Assert.Equal(403, ex.StatusCode);
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
        _repoMock.Setup(r => r.ObterMetaSimplesPorClienteEAnoAsync(clienteId, 2026)).ReturnsAsync((MetaAnual?)null);
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
        _repoMock.Setup(r => r.ObterMetaSimplesPorClienteEAnoAsync(clienteId, 2026)).ReturnsAsync(existente);
        _repoMock.Setup(r => r.SalvarAsync(It.IsAny<MetaAnual>())).ReturnsAsync((MetaAnual m) => m);
        var resultado = await _sut.SalvarMetaAsync(dto, clienteId, "cliente");
        Assert.Equal(200000m, resultado.MetaReceita);
    }

    // ── Objetivos (modo "metodo") — vários por cliente, sem identidade por ano ──────────────

    [Fact]
    public async Task Salvar_ObjetivoSemDataAlvo_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var dto = new SalvarMetaAnualDto { ClienteId = clienteId, Ano = 2026, ModoMeta = "metodo", ValorSonho = 10000m };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.SalvarMetaAsync(dto, clienteId, "cliente"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    [Fact]
    public async Task Salvar_ObjetivoNovoSemId_CriaSemChecarUnicidadePorAno()
    {
        var clienteId = Guid.NewGuid();
        var dto = new SalvarMetaAnualDto
        {
            ClienteId = clienteId, Ano = 2026, ModoMeta = "metodo",
            Sonho = "Macbook", ValorSonho = 15000m, DataAlvo = new DateOnly(2026, 12, 1),
        };
        _repoMock.Setup(r => r.SalvarAsync(It.IsAny<MetaAnual>())).ReturnsAsync((MetaAnual m) => m);

        var resultado = await _sut.SalvarMetaAsync(dto, clienteId, "cliente");

        Assert.Equal("Macbook", resultado.Sonho);
        Assert.Equal(new DateOnly(2026, 12, 1), resultado.DataAlvo);
        _repoMock.Verify(r => r.ObterMetaSimplesPorClienteEAnoAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
        _repoMock.Verify(r => r.ObterPorIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Salvar_ObjetivoComId_AtualizaOMesmoRegistro()
    {
        var clienteId = Guid.NewGuid();
        var objetivoId = Guid.NewGuid();
        var existente = new MetaAnual
        {
            Id = objetivoId, ClienteId = clienteId, ModoMeta = "metodo", Ano = 2026,
            Sonho = "Macbook", ValorSonho = 15000m, DataAlvo = new DateOnly(2026, 12, 1),
            CriadoEm = DateTime.UtcNow, AtualizadoEm = DateTime.UtcNow,
        };
        var dto = new SalvarMetaAnualDto
        {
            Id = objetivoId, ClienteId = clienteId, Ano = 2026, ModoMeta = "metodo",
            Sonho = "Macbook Pro", ValorSonho = 18000m, DataAlvo = new DateOnly(2027, 3, 1),
        };
        _repoMock.Setup(r => r.ObterPorIdAsync(objetivoId)).ReturnsAsync(existente);
        _repoMock.Setup(r => r.SalvarAsync(It.IsAny<MetaAnual>())).ReturnsAsync((MetaAnual m) => m);

        var resultado = await _sut.SalvarMetaAsync(dto, clienteId, "cliente");

        Assert.Equal("Macbook Pro", resultado.Sonho);
        Assert.Equal(18000m, resultado.ValorSonho);
        Assert.Equal(new DateOnly(2027, 3, 1), resultado.DataAlvo);
    }

    [Fact]
    public async Task Salvar_ObjetivoComIdInexistente_LancaNaoEncontrada()
    {
        var clienteId = Guid.NewGuid();
        var dto = new SalvarMetaAnualDto
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Ano = 2026, ModoMeta = "metodo",
            ValorSonho = 1000m, DataAlvo = new DateOnly(2026, 12, 1),
        };
        _repoMock.Setup(r => r.ObterPorIdAsync(dto.Id!.Value)).ReturnsAsync((MetaAnual?)null);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.SalvarMetaAsync(dto, clienteId, "cliente"));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task Salvar_ObjetivoComIdDeOutroCliente_LancaAcessoNegado()
    {
        var clienteId = Guid.NewGuid();
        var objetivoId = Guid.NewGuid();
        var deOutroCliente = new MetaAnual { Id = objetivoId, ClienteId = Guid.NewGuid(), ModoMeta = "metodo" };
        var dto = new SalvarMetaAnualDto
        {
            Id = objetivoId, ClienteId = clienteId, Ano = 2026, ModoMeta = "metodo",
            ValorSonho = 1000m, DataAlvo = new DateOnly(2026, 12, 1),
        };
        _repoMock.Setup(r => r.ObterPorIdAsync(objetivoId)).ReturnsAsync(deOutroCliente);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.SalvarMetaAsync(dto, clienteId, "cliente"));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Excluir_MetaExistenteDoProprioCliente_Remove()
    {
        var clienteId = Guid.NewGuid();
        var meta = new MetaAnual { Id = Guid.NewGuid(), ClienteId = clienteId, ModoMeta = "metodo" };
        _repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);

        await _sut.ExcluirMetaAsync(meta.Id, clienteId, "cliente");

        _repoMock.Verify(r => r.RemoverAsync(meta), Times.Once);
    }

    [Fact]
    public async Task Excluir_MetaDeOutroCliente_LancaAcessoNegado()
    {
        var meta = new MetaAnual { Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), ModoMeta = "metodo" };
        var repoMock = new Mock<IMetaRepository>();
        repoMock.Setup(r => r.ObterPorIdAsync(meta.Id)).ReturnsAsync(meta);
        var sut = new MetaService(repoMock.Object, _metaProgressoMock.Object);

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.ExcluirMetaAsync(meta.Id, Guid.NewGuid(), "cliente"));

        Assert.Equal(403, ex.StatusCode);
        repoMock.Verify(r => r.RemoverAsync(It.IsAny<MetaAnual>()), Times.Never);
    }

    [Fact]
    public async Task Excluir_MetaInexistente_LancaNaoEncontrada()
    {
        _repoMock.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>())).ReturnsAsync((MetaAnual?)null);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ExcluirMetaAsync(Guid.NewGuid(), Guid.NewGuid(), "admin"));

        Assert.Equal(404, ex.StatusCode);
    }
}
