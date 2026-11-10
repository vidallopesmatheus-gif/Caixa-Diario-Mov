using CaixaDiario.API.DTOs.Auth;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace CaixaDiario.Tests.Services;

// Fake em memória do repositório de refresh tokens — a rotação depende do que já foi gravado.
internal sealed class RefreshTokenRepositoryFake : IRefreshTokenRepository
{
    public List<RefreshToken> Tokens { get; } = new();

    public Task<RefreshToken?> ObterPorHashAsync(string tokenHash) => Task.FromResult(Tokens.FirstOrDefault(t => t.TokenHash == tokenHash));
    public Task AdicionarAsync(RefreshToken token) { Tokens.Add(token); return Task.CompletedTask; }
    public Task AtualizarAsync(RefreshToken token) => Task.CompletedTask;

    public Task RevogarFamiliaAsync(Guid familiaId, DateTime agora)
    {
        foreach (var t in Tokens.Where(t => t.FamiliaId == familiaId && t.RevogadoEm == null)) t.RevogadoEm = agora;
        return Task.CompletedTask;
    }

    public Task RemoverExpiradosDoUsuarioAsync(Guid usuarioId, DateTime agora)
    {
        Tokens.RemoveAll(t => t.UsuarioId == usuarioId && t.ExpiraEm < agora);
        return Task.CompletedTask;
    }
}

public class AuthServiceSessaoTests
{
    private readonly Mock<IUsuarioRepository> _usuarioRepo = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly RefreshTokenRepositoryFake _refreshRepo = new();
    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero));
    private readonly Usuario _usuario;
    private readonly AuthService _sut;

    public AuthServiceSessaoTests()
    {
        _usuario = new Usuario
        {
            Id = Guid.NewGuid(), NomeUsuario = "joao", Nome = "João", Perfil = "cliente", Ativo = true,
            SenhaHash = BCrypt.Net.BCrypt.HashPassword("senha123"),
        };
        _usuarioRepo.Setup(r => r.ObterPorNomeUsuarioAsync("joao")).ReturnsAsync(_usuario);
        _usuarioRepo.Setup(r => r.ObterPorIdAsync(_usuario.Id)).ReturnsAsync(_usuario);
        _tokenService.Setup(t => t.GerarTokenAcesso(It.IsAny<Usuario>())).Returns(() => new TokenAcesso(Guid.NewGuid().ToString(), DateTime.UtcNow.AddMinutes(30)));
        _sut = new AuthService(_usuarioRepo.Object, _tokenService.Object, _refreshRepo, config: null, _relogio);
    }

    private Task<SessaoEmitida> LoginAsync() => _sut.LoginAsync(new LoginRequestDto { NomeUsuario = "joao", Senha = "senha123" });

    [Fact]
    public async Task Login_EmiteRefreshTokenQueNoBancoFicaSoComoHash()
    {
        var sessao = await LoginAsync();

        var gravado = Assert.Single(_refreshRepo.Tokens);
        Assert.NotNull(sessao.RefreshToken);
        Assert.NotEqual(sessao.RefreshToken, gravado.TokenHash);
        Assert.Equal(TokenService.HashRefreshToken(sessao.RefreshToken!), gravado.TokenHash);
        Assert.Equal(_relogio.GetUtcNow().UtcDateTime.AddDays(14), sessao.RefreshExpiraEm);
    }

    [Fact]
    public async Task Renovar_TokenValido_TrocaPorUmNovoNaMesmaFamiliaERevogaOAntigo()
    {
        var login = await LoginAsync();

        var renovada = await _sut.RenovarAsync(login.RefreshToken);

        Assert.NotEqual(login.RefreshToken, renovada.RefreshToken);
        var (antigo, novo) = (_refreshRepo.Tokens[0], _refreshRepo.Tokens[1]);
        Assert.True(antigo.Substituido);
        Assert.NotNull(antigo.RevogadoEm);
        Assert.Null(novo.RevogadoEm);
        Assert.Equal(antigo.FamiliaId, novo.FamiliaId);
        Assert.Equal(_usuario.Id, renovada.Resposta.UsuarioId);
    }

    [Fact]
    public async Task Renovar_DuasAbasComOMesmoCookieAoMesmoTempo_NaoDerrubaASessao()
    {
        var login = await LoginAsync();
        await _sut.RenovarAsync(login.RefreshToken);
        _relogio.Advance(TimeSpan.FromSeconds(5));

        var segundaAba = await _sut.RenovarAsync(login.RefreshToken);

        Assert.NotNull(segundaAba.RefreshToken);
    }

    [Fact]
    public async Task Renovar_TokenJaTrocadoReusadoDepoisDaJanela_RevogaAFamiliaInteira()
    {
        var login = await LoginAsync();
        var renovada = await _sut.RenovarAsync(login.RefreshToken);
        _relogio.Advance(TimeSpan.FromMinutes(5));

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.RenovarAsync(login.RefreshToken));

        Assert.Equal((401, CodigoRetorno.SESSAO_EXPIRADA), (ex.StatusCode, ex.Codigo));
        await Assert.ThrowsAsync<ApiException>(() => _sut.RenovarAsync(renovada.RefreshToken));
    }

    [Fact]
    public async Task Renovar_RefreshVencido_LancaSessaoExpirada()
    {
        var login = await LoginAsync();
        _relogio.Advance(TimeSpan.FromDays(15));

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.RenovarAsync(login.RefreshToken));

        Assert.Equal(CodigoRetorno.SESSAO_EXPIRADA, ex.Codigo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("cookie-que-nunca-existiu")]
    public async Task Renovar_SemCookieOuDesconhecido_LancaSessaoExpirada(string? cookie)
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.RenovarAsync(cookie));
        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task Renovar_UsuarioDesativadoDepoisDoLogin_LancaSessaoExpirada()
    {
        var login = await LoginAsync();
        _usuario.Ativo = false;

        await Assert.ThrowsAsync<ApiException>(() => _sut.RenovarAsync(login.RefreshToken));

        Assert.All(_refreshRepo.Tokens, t => Assert.NotNull(t.RevogadoEm));
    }

    [Fact]
    public async Task Encerrar_RevogaASessaoENaoDaMaisPraRenovar()
    {
        var login = await LoginAsync();

        await _sut.EncerrarAsync(login.RefreshToken);

        await Assert.ThrowsAsync<ApiException>(() => _sut.RenovarAsync(login.RefreshToken));
    }
}
