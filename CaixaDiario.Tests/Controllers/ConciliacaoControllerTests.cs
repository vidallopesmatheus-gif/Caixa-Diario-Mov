using System.Security.Claims;
using CaixaDiario.API.Controllers;
using CaixaDiario.API.DTOs.Conciliacao;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CaixaDiario.Tests.Controllers;

public class ConciliacaoControllerTests
{
    private readonly Mock<IConciliacaoService> _serviceMock = new();

    private ConciliacaoController CriarSut(Guid usuarioId, string perfil)
    {
        var sut = new ConciliacaoController(_serviceMock.Object);
        var claims = new[] { new Claim("id", usuarioId.ToString()), new Claim("perfil", perfil) };
        sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims)) }
        };
        return sut;
    }

    [Fact]
    public async Task ListarSugestoes_RepassaParametrosEDevolveOk()
    {
        var clienteId = Guid.NewGuid();
        var de = new DateOnly(2026, 10, 1);
        var ate = new DateOnly(2026, 10, 31);
        var lista = new List<SugestaoVinculoDto> { new() { ContaProvisionadaId = Guid.NewGuid() } };
        _serviceMock.Setup(s => s.ListarSugestoesAsync(clienteId, de, ate, clienteId, "cliente")).ReturnsAsync(lista);

        var result = await CriarSut(clienteId, "cliente").ListarSugestoes(clienteId, de, ate);

        var ok = Assert.IsType<OkObjectResult>(result);
        var resposta = Assert.IsType<ApiResponse<List<SugestaoVinculoDto>>>(ok.Value);
        Assert.Same(lista, resposta.Dados);
    }
}
