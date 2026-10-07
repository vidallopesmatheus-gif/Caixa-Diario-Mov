using System.Security.Claims;
using CaixaDiario.API.Controllers;
using CaixaDiario.API.DTOs.Relatorios;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CaixaDiario.Tests.Controllers;

public class RelatoriosControllerTests
{
    private readonly Mock<IPrevistoRealizadoService> _serviceMock = new();

    private RelatoriosController CriarSut(Guid usuarioId, string perfil)
    {
        var sut = new RelatoriosController(_serviceMock.Object);
        var claims = new[] { new Claim("id", usuarioId.ToString()), new Claim("perfil", perfil) };
        sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims)) }
        };
        return sut;
    }

    [Fact]
    public async Task ObterPrevistoRealizado_RepassaMesesEDevolveOk()
    {
        var clienteId = Guid.NewGuid();
        var dto = new PrevistoRealizadoDto();
        _serviceMock.Setup(s => s.ObterAsync(clienteId, 12, clienteId, "cliente")).ReturnsAsync(dto);

        var result = await CriarSut(clienteId, "cliente").ObterPrevistoRealizado(clienteId, 12);

        var ok = Assert.IsType<OkObjectResult>(result);
        var resposta = Assert.IsType<ApiResponse<PrevistoRealizadoDto>>(ok.Value);
        Assert.Same(dto, resposta.Dados);
    }

    [Fact]
    public async Task ObterPrevistoRealizado_SemMesesInformado_UsaPadraoDe6()
    {
        var clienteId = Guid.NewGuid();
        var dto = new PrevistoRealizadoDto();
        _serviceMock.Setup(s => s.ObterAsync(clienteId, 6, clienteId, "cliente")).ReturnsAsync(dto);

        await CriarSut(clienteId, "cliente").ObterPrevistoRealizado(clienteId);

        _serviceMock.Verify(s => s.ObterAsync(clienteId, 6, clienteId, "cliente"), Times.Once);
    }
}
