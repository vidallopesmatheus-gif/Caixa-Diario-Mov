using CaixaDiario.API.DTOs.Usuarios;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class PreferenciasFuturoServiceTests
{
    private readonly Mock<IUsuarioRepository> _repoMock = new();
    private readonly PreferenciasFuturoService _sut;

    public PreferenciasFuturoServiceTests()
    {
        _sut = new PreferenciasFuturoService(_repoMock.Object);
    }

    private static Usuario CriarUsuario(Guid id, decimal? reservaMinima = null, string? tipoPessoa = null) => new()
    {
        Id = id, NomeUsuario = "cliente1", SenhaHash = "x", Nome = "Cliente Um", Perfil = "cliente",
        Ativo = true, CriadoEm = DateTime.UtcNow, ReservaMinima = reservaMinima, TipoPessoa = tipoPessoa,
    };

    [Fact]
    public async Task ObterAsync_UsuarioComPreferenciasSalvas_RetornaOsValores()
    {
        var clienteId = Guid.NewGuid();
        _repoMock.Setup(r => r.ObterPorIdAsync(clienteId)).ReturnsAsync(CriarUsuario(clienteId, 2000m, "PF"));

        var resultado = await _sut.ObterAsync(clienteId, clienteId, "cliente");

        Assert.Equal(2000m, resultado.ReservaMinima);
        Assert.Equal("PF", resultado.TipoPessoa);
    }

    [Fact]
    public async Task ObterAsync_ClienteAcessandoOutro_LancaAcessoNegado()
    {
        var clienteId = Guid.NewGuid();
        var outroUsuarioId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ObterAsync(clienteId, outroUsuarioId, "cliente"));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task ObterAsync_UsuarioNaoEncontrado_LancaErro()
    {
        var clienteId = Guid.NewGuid();
        _repoMock.Setup(r => r.ObterPorIdAsync(clienteId)).ReturnsAsync((Usuario?)null);

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.ObterAsync(clienteId, clienteId, "cliente"));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task AtualizarAsync_DadosValidos_PersisteESeiBatRetorno()
    {
        var clienteId = Guid.NewGuid();
        var usuario = CriarUsuario(clienteId);
        _repoMock.Setup(r => r.ObterPorIdAsync(clienteId)).ReturnsAsync(usuario);
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<Usuario>())).ReturnsAsync((Usuario u) => u);

        var resultado = await _sut.AtualizarAsync(clienteId, clienteId, "cliente", new AtualizarPreferenciasFuturoDto { ReservaMinima = 3000m, TipoPessoa = "PJ" });

        Assert.Equal(3000m, resultado.ReservaMinima);
        Assert.Equal("PJ", resultado.TipoPessoa);
        _repoMock.Verify(r => r.AtualizarAsync(It.Is<Usuario>(u => u.ReservaMinima == 3000m && u.TipoPessoa == "PJ")), Times.Once);
    }

    [Fact]
    public async Task AtualizarAsync_ComReservaMinimaNegativa_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        _repoMock.Setup(r => r.ObterPorIdAsync(clienteId)).ReturnsAsync(CriarUsuario(clienteId));

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.AtualizarAsync(clienteId, clienteId, "cliente", new AtualizarPreferenciasFuturoDto { ReservaMinima = -1m }));

        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    [Fact]
    public async Task AtualizarAsync_ComTipoPessoaInvalido_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        _repoMock.Setup(r => r.ObterPorIdAsync(clienteId)).ReturnsAsync(CriarUsuario(clienteId));

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.AtualizarAsync(clienteId, clienteId, "cliente", new AtualizarPreferenciasFuturoDto { TipoPessoa = "ME" }));

        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }

    [Fact]
    public async Task AtualizarAsync_ComTipoPessoaNulo_LimpaAPreferencia()
    {
        var clienteId = Guid.NewGuid();
        _repoMock.Setup(r => r.ObterPorIdAsync(clienteId)).ReturnsAsync(CriarUsuario(clienteId, 500m, "PF"));
        _repoMock.Setup(r => r.AtualizarAsync(It.IsAny<Usuario>())).ReturnsAsync((Usuario u) => u);

        var resultado = await _sut.AtualizarAsync(clienteId, clienteId, "cliente", new AtualizarPreferenciasFuturoDto { ReservaMinima = null, TipoPessoa = null });

        Assert.Null(resultado.ReservaMinima);
        Assert.Null(resultado.TipoPessoa);
    }

    [Fact]
    public async Task AtualizarAsync_ClienteAcessandoOutro_LancaAcessoNegado()
    {
        var clienteId = Guid.NewGuid();
        var outroUsuarioId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.AtualizarAsync(clienteId, outroUsuarioId, "cliente", new AtualizarPreferenciasFuturoDto()));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task ObterAsync_Admin_PodeAcessarPreferenciasDeQualquerCliente()
    {
        var clienteId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        _repoMock.Setup(r => r.ObterPorIdAsync(clienteId)).ReturnsAsync(CriarUsuario(clienteId, 1000m, "PJ"));

        var resultado = await _sut.ObterAsync(clienteId, adminId, "admin");

        Assert.Equal(1000m, resultado.ReservaMinima);
    }
}
