using System.Security.Claims;
using CaixaDiario.API.Controllers;
using CaixaDiario.API.DTOs.Projecao;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CaixaDiario.Tests.Controllers;

public class ProjecaoControllerTests
{
    private readonly Mock<IProjecaoService> _projecaoMock = new();
    private readonly Mock<IRegistroRepository> _registroMock = new();
    private readonly Mock<IContaRecorrenteRepository> _contaMock = new();

    private ProjecaoController CriarSut(Guid usuarioId, string perfil)
    {
        var sut = new ProjecaoController(_projecaoMock.Object, _registroMock.Object, _contaMock.Object);
        var claims = new[] { new Claim("id", usuarioId.ToString()), new Claim("perfil", perfil) };
        sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims)) }
        };
        return sut;
    }

    private void ConfigurarRepositorios(Guid clienteId)
    {
        _registroMock.Setup(r => r.ListarPorClienteAsync(clienteId)).ReturnsAsync(new List<RegistroDiario>());
        _contaMock.Setup(r => r.ListarAtivasPorClienteAsync(clienteId)).ReturnsAsync(new List<ContaRecorrente>());
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(90)]
    public async Task ObterProjecao_ComDiasValido_UsaValorInformado(int dias)
    {
        var clienteId = Guid.NewGuid();
        ConfigurarRepositorios(clienteId);
        int? diasRecebidos = null;
        _projecaoMock.Setup(s => s.Calcular(It.IsAny<List<RegistroDiario>>(), It.IsAny<List<ContaRecorrente>>(), It.IsAny<int>(), It.IsAny<Guid?>()))
            .Callback<List<RegistroDiario>, List<ContaRecorrente>, int, Guid?>((_, _, d, _) => diasRecebidos = d)
            .Returns(new ProjecaoDto());

        await CriarSut(Guid.NewGuid(), "admin").ObterProjecao(clienteId, dias);

        Assert.Equal(dias, diasRecebidos);
    }

    [Fact]
    public async Task ObterProjecao_ComDiasInvalido_NormalizaPara30()
    {
        var clienteId = Guid.NewGuid();
        ConfigurarRepositorios(clienteId);
        int? diasRecebidos = null;
        _projecaoMock.Setup(s => s.Calcular(It.IsAny<List<RegistroDiario>>(), It.IsAny<List<ContaRecorrente>>(), It.IsAny<int>(), It.IsAny<Guid?>()))
            .Callback<List<RegistroDiario>, List<ContaRecorrente>, int, Guid?>((_, _, d, _) => diasRecebidos = d)
            .Returns(new ProjecaoDto());

        await CriarSut(Guid.NewGuid(), "admin").ObterProjecao(clienteId, 45);

        Assert.Equal(30, diasRecebidos);
    }

    [Fact]
    public async Task ObterProjecao_Admin_RetornaOk()
    {
        var clienteId = Guid.NewGuid();
        ConfigurarRepositorios(clienteId);
        _projecaoMock.Setup(s => s.Calcular(It.IsAny<List<RegistroDiario>>(), It.IsAny<List<ContaRecorrente>>(), It.IsAny<int>(), It.IsAny<Guid?>()))
            .Returns(new ProjecaoDto());

        var result = await CriarSut(Guid.NewGuid(), "admin").ObterProjecao(clienteId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<ApiResponse<ProjecaoDto>>(ok.Value);
    }

    [Fact]
    public async Task ObterProjecao_ClienteAcessandoOutro_LancaAcessoNegado()
    {
        var sut = CriarSut(Guid.NewGuid(), "cliente");

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.ObterProjecao(Guid.NewGuid()));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task ObterTrajetoria_Admin_RetornaOk()
    {
        var clienteId = Guid.NewGuid();
        ConfigurarRepositorios(clienteId);
        _projecaoMock.Setup(s => s.CalcularTrajetoria(It.IsAny<List<RegistroDiario>>(), It.IsAny<List<ContaRecorrente>>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Guid?>()))
            .Returns(new TrajetoriaDto());

        var result = await CriarSut(Guid.NewGuid(), "admin").ObterTrajetoria(clienteId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<ApiResponse<TrajetoriaDto>>(ok.Value);
    }

    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(30, 30, 12, 12)]
    public async Task ObterTrajetoria_ComMesesForaDoIntervalo_ClampaEntre1E12(int mesesPassado, int mesesFuturo, int passadoEsperado, int futuroEsperado)
    {
        var clienteId = Guid.NewGuid();
        ConfigurarRepositorios(clienteId);
        (int Passado, int Futuro)? recebido = null;
        _projecaoMock.Setup(s => s.CalcularTrajetoria(It.IsAny<List<RegistroDiario>>(), It.IsAny<List<ContaRecorrente>>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Guid?>()))
            .Callback<List<RegistroDiario>, List<ContaRecorrente>, int, int, Guid?>((_, _, p, f, _) => recebido = (p, f))
            .Returns(new TrajetoriaDto());

        await CriarSut(Guid.NewGuid(), "admin").ObterTrajetoria(clienteId, mesesPassado, mesesFuturo);

        Assert.Equal((passadoEsperado, futuroEsperado), recebido);
    }

    [Fact]
    public async Task ObterTrajetoria_ClienteAcessandoOutro_LancaAcessoNegado()
    {
        var sut = CriarSut(Guid.NewGuid(), "cliente");

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.ObterTrajetoria(Guid.NewGuid()));

        Assert.Equal(403, ex.StatusCode);
    }
}
