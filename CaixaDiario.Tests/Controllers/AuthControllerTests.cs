// CaixaDiario.Tests/Controllers/AuthControllerTests.cs
using CaixaDiario.API.Controllers;
using CaixaDiario.API.DTOs.Auth;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Moq;

namespace CaixaDiario.Tests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IAuthService> _serviceMock = new();
    private readonly Mock<IHostEnvironment> _envMock = new();
    private readonly AuthController _sut;

    public AuthControllerTests()
    {
        _envMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        _sut = new AuthController(_serviceMock.Object, _envMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    private static SessaoEmitida Sessao(string token = "jwt-token") => new(
        new LoginResponseDto { Token = token, NomeCompleto = "João", Perfil = "cliente", UsuarioId = Guid.NewGuid() },
        "refresh-bruto", new DateTime(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc));

    private string CookieGravado() => _sut.HttpContext.Response.Headers.SetCookie.ToString();

    [Fact]
    public async Task Login_CredenciaisValidas_RetornaOkComToken()
    {
        _serviceMock.Setup(s => s.LoginAsync(It.IsAny<LoginRequestDto>())).ReturnsAsync(Sessao());

        var result = await _sut.Login(new LoginRequestDto { NomeUsuario = "joao", Senha = "senha" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponse<LoginResponseDto>>(ok.Value);
        Assert.Equal("jwt-token", body.Dados!.Token);
        Assert.Equal("João", body.Dados.NomeCompleto);
    }

    // O refresh token só vai no cookie — nunca no corpo, que o JS (e um XSS) consegue ler.
    [Fact]
    public async Task Login_GravaRefreshNumCookieHttpOnlySecureSameSiteStrictRestritoAAuth()
    {
        _serviceMock.Setup(s => s.LoginAsync(It.IsAny<LoginRequestDto>())).ReturnsAsync(Sessao());

        await _sut.Login(new LoginRequestDto { NomeUsuario = "joao", Senha = "senha" });

        var cookie = CookieGravado().ToLowerInvariant();
        Assert.StartsWith($"{AuthController.CookieRefresh}=refresh-bruto", cookie);
        Assert.Contains("httponly", cookie);
        Assert.Contains("secure", cookie);
        Assert.Contains("samesite=strict", cookie);
        Assert.Contains("path=/api/auth", cookie);
    }

    [Fact]
    public async Task Refresh_ComCookie_DevolveTokenNovoEGravaOCookieNovo()
    {
        _sut.HttpContext.Request.Headers.Cookie = $"{AuthController.CookieRefresh}=refresh-antigo";
        _serviceMock.Setup(s => s.RenovarAsync("refresh-antigo")).ReturnsAsync(Sessao("jwt-novo"));

        var result = await _sut.Refresh();

        var body = Assert.IsType<ApiResponse<LoginResponseDto>>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal("jwt-novo", body.Dados!.Token);
        Assert.Contains("refresh-bruto", CookieGravado());
    }

    [Fact]
    public async Task Refresh_SessaoExpirada_ApagaOCookieEPropaga401()
    {
        _serviceMock.Setup(s => s.RenovarAsync(It.IsAny<string?>()))
            .ThrowsAsync(new ApiException(401, CodigoRetorno.SESSAO_EXPIRADA, "Sua sessão expirou."));

        var ex = await Assert.ThrowsAsync<ApiException>(() => _sut.Refresh());

        Assert.Equal(401, ex.StatusCode);
        Assert.Contains("expires=thu, 01 jan 1970", CookieGravado().ToLowerInvariant());
    }

    [Fact]
    public async Task Logout_EncerraASessaoDoCookieEApagaOCookie()
    {
        _sut.HttpContext.Request.Headers.Cookie = $"{AuthController.CookieRefresh}=refresh-atual";

        await _sut.Logout();

        _serviceMock.Verify(s => s.EncerrarAsync("refresh-atual"), Times.Once);
        Assert.Contains("expires=thu, 01 jan 1970", CookieGravado().ToLowerInvariant());
    }

    [Fact]
    public async Task Login_PropagaExcecaoDoServico()
    {
        _serviceMock.Setup(s => s.LoginAsync(It.IsAny<LoginRequestDto>()))
            .ThrowsAsync(new ApiException(401, CodigoRetorno.CREDENCIAIS_INVALIDAS, "Credenciais inválidas"));

        await Assert.ThrowsAsync<ApiException>(() => _sut.Login(new LoginRequestDto { NomeUsuario = "x", Senha = "y" }));
    }

    // Item 2.10: login precisa continuar público mesmo que uma fallback policy de autorização
    // global seja adicionada depois, e mesmo com um token antigo/inválido de outra sessão no
    // header — [AllowAnonymous] garante isso explicitamente, não só "não ter [Authorize]".
    // Refresh/logout idem: quem autentica ali é o cookie, não o JWT (que pode já ter vencido).
    [Theory]
    [InlineData(nameof(AuthController.Login))]
    [InlineData(nameof(AuthController.Refresh))]
    [InlineData(nameof(AuthController.Logout))]
    public void RotasDeSessao_TemAllowAnonymous(string acao)
    {
        var metodo = typeof(AuthController).GetMethod(acao)!;
        Assert.True(metodo.IsDefined(typeof(AllowAnonymousAttribute), inherit: false));
    }
}
