using System.Security.Claims;
using CaixaDiario.API.Controllers;
using CaixaDiario.API.DTOs.Insights;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CaixaDiario.Tests.Controllers;

public class InsightsControllerTests
{
    private readonly Mock<IInsightService> _insightMock = new();
    private readonly Mock<IRegistroRepository> _registroMock = new();
    private readonly Mock<IContaRecorrenteRepository> _contaMock = new();
    private readonly Mock<IMetaRepository> _metaMock = new();

    private InsightsController CriarSut(Guid usuarioId, string perfil)
    {
        var sut = new InsightsController(_insightMock.Object, _registroMock.Object, _contaMock.Object, _metaMock.Object);
        var claims = new[] { new Claim("id", usuarioId.ToString()), new Claim("perfil", perfil) };
        sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims)) }
        };
        return sut;
    }

    [Fact]
    public async Task ObterInsights_Admin_RetornaOk()
    {
        var clienteId = Guid.NewGuid();
        _registroMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario>());
        _contaMock.Setup(r => r.ListarAtivasPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaRecorrente>());
        _metaMock.Setup(r => r.ObterPorClienteEAnoAsync(clienteId, It.IsAny<int>())).ReturnsAsync((MetaAnual?)null);
        _insightMock.Setup(s => s.Calcular(It.IsAny<List<RegistroDiario>>(), It.IsAny<List<ContaRecorrente>>(), It.IsAny<MetaAnual?>()))
            .Returns(new List<InsightDto>());

        var result = await CriarSut(Guid.NewGuid(), "admin").ObterInsights(clienteId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<ApiResponse<List<InsightDto>>>(ok.Value);
    }

    [Fact]
    public async Task ObterInsights_ClienteProprio_RetornaOk()
    {
        var clienteId = Guid.NewGuid();
        _registroMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario>());
        _contaMock.Setup(r => r.ListarAtivasPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaRecorrente>());
        _metaMock.Setup(r => r.ObterPorClienteEAnoAsync(clienteId, It.IsAny<int>())).ReturnsAsync((MetaAnual?)null);
        _insightMock.Setup(s => s.Calcular(It.IsAny<List<RegistroDiario>>(), It.IsAny<List<ContaRecorrente>>(), It.IsAny<MetaAnual?>()))
            .Returns(new List<InsightDto>());

        var result = await CriarSut(clienteId, "cliente").ObterInsights(clienteId);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task ObterInsights_ClienteAcessandoOutro_LancaAcessoNegado()
    {
        var sut = CriarSut(Guid.NewGuid(), "cliente");

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.ObterInsights(Guid.NewGuid()));

        Assert.Equal(403, ex.StatusCode);
    }
}
