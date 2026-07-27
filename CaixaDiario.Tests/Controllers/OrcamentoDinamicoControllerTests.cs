using System.Security.Claims;
using CaixaDiario.API.Controllers;
using CaixaDiario.API.DTOs.OrcamentoDinamico;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CaixaDiario.Tests.Controllers;

public class OrcamentoDinamicoControllerTests
{
    private readonly Mock<IOrcamentoDinamicoService> _orcamentoMock = new();
    private readonly Mock<IRegistroRepository> _registroMock = new();
    private readonly Mock<IContaRecorrenteRepository> _contaMock = new();
    private readonly Mock<IMetaRepository> _metaMock = new();

    private OrcamentoDinamicoController CriarSut(Guid usuarioId, string perfil)
    {
        var sut = new OrcamentoDinamicoController(_orcamentoMock.Object, _registroMock.Object, _contaMock.Object, _metaMock.Object);
        var claims = new[] { new Claim("id", usuarioId.ToString()), new Claim("perfil", perfil) };
        sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims)) }
        };
        return sut;
    }

    [Fact]
    public async Task ObterOrcamento_Admin_RetornaOk()
    {
        var clienteId = Guid.NewGuid();
        _registroMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario>());
        _contaMock.Setup(r => r.ListarAtivasPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaRecorrente>());
        _metaMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<MetaAnual>());
        _orcamentoMock.Setup(s => s.Calcular(It.IsAny<List<RegistroDiario>>(), It.IsAny<List<ContaRecorrente>>(), It.IsAny<List<MetaAnual>>()))
            .Returns(new OrcamentoDinamicoDto());

        var result = await CriarSut(Guid.NewGuid(), "admin").ObterOrcamento(clienteId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<ApiResponse<OrcamentoDinamicoDto>>(ok.Value);
    }

    [Fact]
    public async Task ObterOrcamento_ClienteProprio_RetornaOk()
    {
        var clienteId = Guid.NewGuid();
        _registroMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario>());
        _contaMock.Setup(r => r.ListarAtivasPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaRecorrente>());
        _metaMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<MetaAnual>());
        _orcamentoMock.Setup(s => s.Calcular(It.IsAny<List<RegistroDiario>>(), It.IsAny<List<ContaRecorrente>>(), It.IsAny<List<MetaAnual>>()))
            .Returns(new OrcamentoDinamicoDto());

        var result = await CriarSut(clienteId, "cliente").ObterOrcamento(clienteId);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task ObterOrcamento_ClienteAcessandoOutro_LancaAcessoNegado()
    {
        var sut = CriarSut(Guid.NewGuid(), "cliente");

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.ObterOrcamento(Guid.NewGuid()));

        Assert.Equal(403, ex.StatusCode);
    }
}
