// CaixaDiario.Tests/Controllers/AuthControllerTests.cs
using CaixaDiario.API.Controllers;
using CaixaDiario.API.DTOs.Auth;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CaixaDiario.Tests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IAuthService> _serviceMock = new();
    private readonly AuthController _sut;

    public AuthControllerTests()
    {
        _sut = new AuthController(_serviceMock.Object);
    }

    [Fact]
    public async Task Login_CredenciaisValidas_RetornaOkComToken()
    {
        var response = new LoginResponseDto
        {
            Token = "jwt-token",
            NomeCompleto = "João",
            Perfil = "cliente",
            UsuarioId = Guid.NewGuid()
        };
        _serviceMock.Setup(s => s.LoginAsync(It.IsAny<LoginRequestDto>())).ReturnsAsync(response);

        var result = await _sut.Login(new LoginRequestDto { NomeUsuario = "joao", Senha = "senha" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponse<LoginResponseDto>>(ok.Value);
        Assert.Equal("jwt-token", body.Dados!.Token);
        Assert.Equal("João", body.Dados.NomeCompleto);
    }

    [Fact]
    public async Task Login_PropagaExcecaoDoServico()
    {
        _serviceMock.Setup(s => s.LoginAsync(It.IsAny<LoginRequestDto>()))
            .ThrowsAsync(new CaixaDiario.API.Exceptions.ApiException(401,
                CaixaDiario.API.Enums.CodigoRetorno.CREDENCIAIS_INVALIDAS, "Credenciais inválidas"));

        await Assert.ThrowsAsync<CaixaDiario.API.Exceptions.ApiException>(
            () => _sut.Login(new LoginRequestDto { NomeUsuario = "x", Senha = "y" }));
    }
}
