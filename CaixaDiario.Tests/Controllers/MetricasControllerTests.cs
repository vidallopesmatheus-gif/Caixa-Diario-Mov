using System.Security.Claims;
using CaixaDiario.API.Controllers;
using CaixaDiario.API.DTOs.Metricas;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CaixaDiario.Tests.Controllers;

public class MetricasControllerTests
{
    private readonly Mock<IMetricasService> _metricasMock = new();
    private readonly Mock<IRegistroRepository> _registroMock = new();
    private readonly Mock<IContaRecorrenteRepository> _contaMock = new();

    private MetricasController CriarSut(Guid usuarioId, string perfil)
    {
        var sut = new MetricasController(_metricasMock.Object, _registroMock.Object, _contaMock.Object);
        var claims = new[] { new Claim("id", usuarioId.ToString()), new Claim("perfil", perfil) };
        sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims)) }
        };
        return sut;
    }

    [Fact]
    public async Task ObterMetricas_Admin_RetornaOk()
    {
        var clienteId = Guid.NewGuid();
        _registroMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario>());
        _metricasMock.Setup(m => m.CalcularPeriodo(It.IsAny<List<RegistroDiario>>(), It.IsAny<List<RegistroDiario>>(), It.IsAny<decimal>()))
            .Returns(new MetricasPeriodoDto());

        var result = await CriarSut(Guid.NewGuid(), "admin")
            .ObterMetricas(clienteId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<ApiResponse<MetricasPeriodoDto>>(ok.Value);
    }

    [Fact]
    public async Task ObterMetricas_ClienteAcessandoOutro_LancaAcessoNegado()
    {
        var sut = CriarSut(Guid.NewGuid(), "cliente");
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.ObterMetricas(Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task ObterEvolucao_Admin_RetornaOk()
    {
        var clienteId = Guid.NewGuid();
        _registroMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario>());
        _metricasMock.Setup(m => m.CalcularEvolucao(It.IsAny<List<RegistroDiario>>(), It.IsAny<int>()))
            .Returns(new List<EvolucaoMensalDto>());

        var result = await CriarSut(Guid.NewGuid(), "admin").ObterEvolucao(clienteId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<ApiResponse<List<EvolucaoMensalDto>>>(ok.Value);
    }

    [Fact]
    public async Task ObterFluxoProjetado_ClienteProprio_RetornaOk()
    {
        var clienteId = Guid.NewGuid();
        _registroMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario>());
        _contaMock.Setup(r => r.ListarAtivasPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaRecorrente>());
        _metricasMock.Setup(m => m.CalcularFluxoProjetado(It.IsAny<List<RegistroDiario>>(), It.IsAny<List<ContaRecorrente>>(), It.IsAny<int>()))
            .Returns(new FluxoProjetadoDto());

        var result = await CriarSut(clienteId, "cliente").ObterFluxoProjetado(clienteId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<ApiResponse<FluxoProjetadoDto>>(ok.Value);
    }

    [Fact]
    public async Task ObterDre_SemFiltroDeConta_RetornaOk()
    {
        var clienteId = Guid.NewGuid();
        _registroMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario>());
        _metricasMock.Setup(m => m.CalcularDre(It.IsAny<List<RegistroDiario>>())).Returns(new DreDto());

        var result = await CriarSut(Guid.NewGuid(), "admin")
            .ObterDre(clienteId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<ApiResponse<DreDto>>(ok.Value);
    }

    [Fact]
    public async Task ObterDre_ComFiltroDeConta_FiltraRegistrosDaContaInformada()
    {
        var clienteId = Guid.NewGuid();
        var contaId = Guid.NewGuid();
        var outraContaId = Guid.NewGuid();
        var data = new DateOnly(2026, 1, 15);
        _registroMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario>
        {
            new() { Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = contaId, Data = data, Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(), CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), ClienteId = clienteId, ContaBancariaId = outraContaId, Data = data, Entradas = new(), Saidas = new(), ContasReceber = new(), ContasPagar = new(), CriadoEm = DateTime.UtcNow, SalvoEm = DateTime.UtcNow },
        });

        List<RegistroDiario>? recebidos = null;
        _metricasMock.Setup(m => m.CalcularDre(It.IsAny<List<RegistroDiario>>()))
            .Callback<List<RegistroDiario>>(r => recebidos = r)
            .Returns(new DreDto());

        await CriarSut(Guid.NewGuid(), "admin").ObterDre(clienteId, data, data, contaId);

        var recebido = Assert.Single(recebidos!);
        Assert.Equal(contaId, recebido.ContaBancariaId);
    }

    [Fact]
    public async Task ObterDre_ClienteAcessandoOutro_LancaAcessoNegado()
    {
        var sut = CriarSut(Guid.NewGuid(), "cliente");
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.ObterDre(Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task ObterIndicadores_Admin_RetornaOk()
    {
        var clienteId = Guid.NewGuid();
        _registroMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario>());
        _metricasMock.Setup(m => m.CalcularIndicadores(It.IsAny<List<RegistroDiario>>(), It.IsAny<int>()))
            .Returns(new IndicadoresDecisaoDto { Dre = new DreDto() });

        var result = await CriarSut(Guid.NewGuid(), "admin").ObterIndicadores(clienteId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<ApiResponse<IndicadoresDecisaoDto>>(ok.Value);
    }

    [Fact]
    public async Task ObterIndicadores_ClienteAcessandoOutro_LancaAcessoNegado()
    {
        var sut = CriarSut(Guid.NewGuid(), "cliente");
        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.ObterIndicadores(Guid.NewGuid()));
        Assert.Equal(403, ex.StatusCode);
    }
}
