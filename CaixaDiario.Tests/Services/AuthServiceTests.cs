using CaixaDiario.API.DTOs.Auth;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<IUsuarioRepository> _repoMock = new();
    private readonly Mock<ITokenService> _tokenMock = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _sut = new AuthService(_repoMock.Object, _tokenMock.Object);
    }

    private static Usuario CriarUsuarioAtivo(string senha = "senha123") => new()
    {
        Id = Guid.NewGuid(),
        NomeUsuario = "joao",
        SenhaHash = BCrypt.Net.BCrypt.HashPassword(senha),
        Nome = "João Silva",
        Perfil = "cliente",
        Ativo = true,
        CriadoEm = DateTime.UtcNow
    };

    [Fact]
    public async Task Login_CredenciaisValidas_RetornaLoginResponse()
    {
        var usuario = CriarUsuarioAtivo("senha123");
        _repoMock.Setup(r => r.ObterPorNomeUsuarioAsync("joao")).ReturnsAsync(usuario);
        _tokenMock.Setup(t => t.GerarToken(usuario)).Returns("token-gerado");

        var resultado = await _sut.LoginAsync(new LoginRequestDto { NomeUsuario = "joao", Senha = "senha123" });

        Assert.Equal("token-gerado", resultado.Token);
        Assert.Equal(usuario.Nome, resultado.Nome);
        Assert.Equal(usuario.Perfil, resultado.Perfil);
        Assert.Equal(usuario.Id, resultado.Id);
    }

    [Fact]
    public async Task Login_SenhaErrada_LancaCredenciaisInvalidas()
    {
        var usuario = CriarUsuarioAtivo("correta");
        _repoMock.Setup(r => r.ObterPorNomeUsuarioAsync("joao")).ReturnsAsync(usuario);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.LoginAsync(new LoginRequestDto { NomeUsuario = "joao", Senha = "errada" }));

        Assert.Equal(401, ex.StatusCode);
        Assert.Equal(CodigoRetorno.CREDENCIAIS_INVALIDAS, ex.Codigo);
    }

    [Fact]
    public async Task Login_UsuarioInexistente_LancaCredenciaisInvalidas()
    {
        _repoMock.Setup(r => r.ObterPorNomeUsuarioAsync("naoexiste")).ReturnsAsync((Usuario?)null);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.LoginAsync(new LoginRequestDto { NomeUsuario = "naoexiste", Senha = "qualquer" }));

        Assert.Equal(401, ex.StatusCode);
        Assert.Equal(CodigoRetorno.CREDENCIAIS_INVALIDAS, ex.Codigo);
    }

    [Fact]
    public async Task Login_UsuarioInativo_LancaUsuarioInativo()
    {
        var usuario = CriarUsuarioAtivo();
        usuario.Ativo = false;
        _repoMock.Setup(r => r.ObterPorNomeUsuarioAsync("joao")).ReturnsAsync(usuario);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.LoginAsync(new LoginRequestDto { NomeUsuario = "joao", Senha = "senha123" }));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.USUARIO_INATIVO, ex.Codigo);
    }

    [Theory]
    [InlineData("", "senha")]
    [InlineData("joao", "")]
    public async Task Login_CamposVazios_LancaDadosInvalidos(string nomeUsuario, string senha)
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _sut.LoginAsync(new LoginRequestDto { NomeUsuario = nomeUsuario, Senha = senha }));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(CodigoRetorno.DADOS_INVALIDOS, ex.Codigo);
    }
}
