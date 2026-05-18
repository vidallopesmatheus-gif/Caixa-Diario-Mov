// CaixaDiario.Tests/Controllers/RegistrosControllerTests.cs
using System.Security.Claims;
using CaixaDiario.API.Controllers;
using CaixaDiario.API.DTOs.Registros;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CaixaDiario.Tests.Controllers;

public class RegistrosControllerTests
{
    private readonly Mock<IRegistroService> _serviceMock = new();
    private readonly RegistrosController _sut;
    private readonly Guid _usuarioId = Guid.NewGuid();

    public RegistrosControllerTests()
    {
        _sut = new RegistrosController(_serviceMock.Object);
        _sut.ControllerContext = CriarContexto(_usuarioId, "admin", "testuser");
    }

    private static ControllerContext CriarContexto(Guid usuarioId, string perfil, string nomeUsuario)
    {
        var claims = new[]
        {
            new Claim("id", usuarioId.ToString()),
            new Claim("perfil", perfil),
            new Claim("nome_usuario", nomeUsuario)
        };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        return new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } };
    }

    private static RegistroDto CriarRegistroDto(Guid clienteId) => new()
    {
        Id = Guid.NewGuid(),
        ClienteId = clienteId,
        Data = DateOnly.FromDateTime(DateTime.UtcNow),
        Inicio = 100m,
        Entrada = 200m,
        Saidas = new(),
        ContasReceber = new(),
        ContasPagar = new(),
        SaldoFinal = 300m,
        SalvoEm = DateTime.UtcNow
    };

    [Fact]
    public async Task Listar_RetornaOkComLista()
    {
        var clienteId = Guid.NewGuid();
        var lista = new List<RegistroDto> { CriarRegistroDto(clienteId) };
        _serviceMock.Setup(s => s.ListarPorClienteAsync(clienteId, _usuarioId, "admin")).ReturnsAsync(lista);

        var result = await _sut.Listar(clienteId);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponse<List<RegistroDto>>>(ok.Value);
        Assert.Single(body.Dados!);
    }

    [Fact]
    public async Task ObterPorData_RetornaOkComRegistro()
    {
        var clienteId = Guid.NewGuid();
        var data = DateOnly.FromDateTime(DateTime.UtcNow);
        var dto = CriarRegistroDto(clienteId);
        _serviceMock.Setup(s => s.ObterPorDataAsync(clienteId, data, _usuarioId, "admin")).ReturnsAsync(dto);

        var result = await _sut.ObterPorData(clienteId, data);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponse<RegistroDto>>(ok.Value);
        Assert.Equal(dto.Id, body.Dados!.Id);
    }

    [Fact]
    public async Task Salvar_RegistroNovo_RetornaCreated201()
    {
        var dto = CriarRegistroDto(Guid.NewGuid());
        var criarDto = new CriarRegistroDto { ClienteId = dto.ClienteId, Data = dto.Data };
        _serviceMock.Setup(s => s.SalvarAsync(It.IsAny<CriarRegistroDto>(), "testuser")).ReturnsAsync((dto, true));

        var result = await _sut.Salvar(criarDto);

        Assert.IsType<CreatedAtActionResult>(result);
    }

    [Fact]
    public async Task Salvar_RegistroExistente_RetornaOk200()
    {
        var dto = CriarRegistroDto(Guid.NewGuid());
        var criarDto = new CriarRegistroDto { ClienteId = dto.ClienteId, Data = dto.Data };
        _serviceMock.Setup(s => s.SalvarAsync(It.IsAny<CriarRegistroDto>(), "testuser")).ReturnsAsync((dto, false));

        var result = await _sut.Salvar(criarDto);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Excluir_RetornaOk()
    {
        var clienteId = Guid.NewGuid();
        var data = DateOnly.FromDateTime(DateTime.UtcNow);
        var excluirDto = new ExcluirRegistroDto { MotivoExclusao = "teste" };
        _serviceMock.Setup(s => s.ExcluirAsync(clienteId, data, "teste", _usuarioId, "admin")).Returns(Task.CompletedTask);

        var result = await _sut.Excluir(clienteId, data, excluirDto);

        Assert.IsType<OkObjectResult>(result);
    }
}
