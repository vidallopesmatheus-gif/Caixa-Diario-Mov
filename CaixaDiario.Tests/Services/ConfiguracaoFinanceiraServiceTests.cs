using CaixaDiario.API.DTOs.ConfiguracaoFinanceira;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class ConfiguracaoFinanceiraServiceTests
{
    private readonly Mock<IUsuarioRepository> _usuarioRepoMock = new();
    private readonly ConfiguracaoFinanceiraService _sut;

    public ConfiguracaoFinanceiraServiceTests()
    {
        _sut = new ConfiguracaoFinanceiraService(_usuarioRepoMock.Object);
    }

    private static Usuario CriarCliente(Guid id, decimal? custoVida = null, decimal taxaRetirada = 4m) => new()
    {
        Id = id, NomeUsuario = "cliente", Nome = "Cliente Teste", Perfil = "cliente",
        CustoVidaMensalManual = custoVida, TaxaRetiradaFire = taxaRetirada,
    };

    [Fact]
    public async Task ObterAsync_ClienteAcessandoProprioId_RetornaConfiguracao()
    {
        var clienteId = Guid.NewGuid();
        _usuarioRepoMock.Setup(r => r.ObterPorIdAsync(clienteId)).ReturnsAsync(CriarCliente(clienteId, 8000m, 4m));

        var resultado = await _sut.ObterAsync(clienteId, clienteId, "cliente");

        Assert.Equal(8000m, resultado.CustoVidaMensalManual);
        Assert.Equal(4m, resultado.TaxaRetiradaFire);
    }

    [Fact]
    public async Task ObterAsync_ClienteAcessandoOutroCliente_LancaAcessoNegado()
    {
        var clienteId = Guid.NewGuid();
        var outroUsuarioId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ObterAsync(clienteId, outroUsuarioId, "cliente"));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.ACESSO_NEGADO, ex.Codigo);
        _usuarioRepoMock.Verify(r => r.ObterPorIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ObterAsync_Admin_AcessaConfiguracaoDeQualquerCliente()
    {
        var clienteId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        _usuarioRepoMock.Setup(r => r.ObterPorIdAsync(clienteId)).ReturnsAsync(CriarCliente(clienteId));

        var resultado = await _sut.ObterAsync(clienteId, adminId, "admin");

        Assert.Null(resultado.CustoVidaMensalManual);
    }

    [Fact]
    public async Task ObterAsync_ClienteInexistente_LancaNaoEncontrado()
    {
        var clienteId = Guid.NewGuid();
        _usuarioRepoMock.Setup(r => r.ObterPorIdAsync(clienteId)).ReturnsAsync((Usuario?)null);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ObterAsync(clienteId, clienteId, "cliente"));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task AtualizarAsync_ValoresValidos_PersisteEDevolveAtualizado()
    {
        var clienteId = Guid.NewGuid();
        var cliente = CriarCliente(clienteId);
        _usuarioRepoMock.Setup(r => r.ObterPorIdAsync(clienteId)).ReturnsAsync(cliente);
        _usuarioRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<Usuario>())).ReturnsAsync((Usuario u) => u);

        var dto = new AtualizarConfiguracaoFinanceiraDto { CustoVidaMensalManual = 9000m, TaxaRetiradaFire = 3.5m };
        var resultado = await _sut.AtualizarAsync(clienteId, dto, clienteId, "cliente");

        Assert.Equal(9000m, resultado.CustoVidaMensalManual);
        Assert.Equal(3.5m, resultado.TaxaRetiradaFire);
        Assert.Equal(9000m, cliente.CustoVidaMensalManual);
    }

    [Fact]
    public async Task AtualizarAsync_CustoVidaNulo_VoltaParaModoAutomatico()
    {
        var clienteId = Guid.NewGuid();
        var cliente = CriarCliente(clienteId, 9000m);
        _usuarioRepoMock.Setup(r => r.ObterPorIdAsync(clienteId)).ReturnsAsync(cliente);
        _usuarioRepoMock.Setup(r => r.AtualizarAsync(It.IsAny<Usuario>())).ReturnsAsync((Usuario u) => u);

        var dto = new AtualizarConfiguracaoFinanceiraDto { CustoVidaMensalManual = null, TaxaRetiradaFire = 4m };
        var resultado = await _sut.AtualizarAsync(clienteId, dto, clienteId, "cliente");

        Assert.Null(resultado.CustoVidaMensalManual);
    }

    [Fact]
    public async Task AtualizarAsync_ClienteAcessandoOutroCliente_LancaAcessoNegado()
    {
        var clienteId = Guid.NewGuid();
        var outroUsuarioId = Guid.NewGuid();
        var dto = new AtualizarConfiguracaoFinanceiraDto { TaxaRetiradaFire = 4m };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.AtualizarAsync(clienteId, dto, outroUsuarioId, "cliente"));

        Assert.Equal(403, ex.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task AtualizarAsync_TaxaRetiradaForaDoIntervalo_LancaDadosInvalidos(decimal taxaInvalida)
    {
        var clienteId = Guid.NewGuid();
        var dto = new AtualizarConfiguracaoFinanceiraDto { TaxaRetiradaFire = taxaInvalida };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.AtualizarAsync(clienteId, dto, clienteId, "cliente"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    [Fact]
    public async Task AtualizarAsync_CustoVidaNegativo_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var dto = new AtualizarConfiguracaoFinanceiraDto { CustoVidaMensalManual = -100m, TaxaRetiradaFire = 4m };

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.AtualizarAsync(clienteId, dto, clienteId, "cliente"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }
}
