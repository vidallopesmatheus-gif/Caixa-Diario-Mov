using System.Security.Claims;
using CaixaDiario.API.Controllers;
using CaixaDiario.API.DTOs.SaudeFinanceira;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CaixaDiario.Tests.Controllers;

public class SaudeFinanceiraControllerTests
{
    private readonly Mock<ISaudeFinanceiraService> _saudeMock = new();
    private readonly Mock<IRegistroRepository> _registroMock = new();
    private readonly Mock<IContaRecorrenteRepository> _contaMock = new();
    private readonly Mock<IMetaRepository> _metaMock = new();

    private SaudeFinanceiraController CriarSut(Guid usuarioId, string perfil)
    {
        var sut = new SaudeFinanceiraController(_saudeMock.Object, _registroMock.Object, _contaMock.Object, _metaMock.Object);
        var claims = new[] { new Claim("id", usuarioId.ToString()), new Claim("perfil", perfil) };
        sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims)) }
        };
        return sut;
    }

    [Fact]
    public async Task ObterSaudeFinanceira_Admin_RetornaOk()
    {
        var clienteId = Guid.NewGuid();
        _registroMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario>());
        _contaMock.Setup(r => r.ListarAtivasPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaRecorrente>());
        _metaMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<MetaAnual>());
        _saudeMock.Setup(s => s.Calcular(It.IsAny<List<RegistroDiario>>(), It.IsAny<List<ContaRecorrente>>(), It.IsAny<List<MetaAnual>>()))
            .Returns(new SaudeFinanceiraDto());

        var result = await CriarSut(Guid.NewGuid(), "admin").ObterSaudeFinanceira(clienteId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<ApiResponse<SaudeFinanceiraDto>>(ok.Value);
    }

    [Fact]
    public async Task ObterSaudeFinanceira_ClienteProprio_RetornaOk()
    {
        var clienteId = Guid.NewGuid();
        _registroMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario>());
        _contaMock.Setup(r => r.ListarAtivasPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaRecorrente>());
        _metaMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<MetaAnual>());
        _saudeMock.Setup(s => s.Calcular(It.IsAny<List<RegistroDiario>>(), It.IsAny<List<ContaRecorrente>>(), It.IsAny<List<MetaAnual>>()))
            .Returns(new SaudeFinanceiraDto());

        var result = await CriarSut(clienteId, "cliente").ObterSaudeFinanceira(clienteId);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task ObterSaudeFinanceira_ClienteAcessandoOutro_LancaAcessoNegado()
    {
        var sut = CriarSut(Guid.NewGuid(), "cliente");

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.ObterSaudeFinanceira(Guid.NewGuid()));

        Assert.Equal(403, ex.StatusCode);
    }
}
