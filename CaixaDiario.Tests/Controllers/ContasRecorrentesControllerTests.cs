using System.Security.Claims;
using CaixaDiario.API.Controllers;
using CaixaDiario.API.DTOs.ContasRecorrentes;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CaixaDiario.Tests.Controllers;

public class ContasRecorrentesControllerTests
{
    private readonly Mock<IContaRecorrenteService> _serviceMock = new();
    private readonly ContasRecorrentesController _sut;
    private readonly Guid _usuarioId = Guid.NewGuid();

    public ContasRecorrentesControllerTests()
    {
        _sut = new ContasRecorrentesController(_serviceMock.Object);
        var claims = new[] { new Claim("id", _usuarioId.ToString()), new Claim("perfil", "admin") };
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims)) }
        };
    }

    private static ContaRecorrenteDto CriarDto(Guid clienteId) => new()
    {
        Id = Guid.NewGuid(), ClienteId = clienteId, Descricao = "Aluguel", Valor = 1000m,
        Tipo = "Pagar", DataInicio = new DateOnly(2026, 1, 1), Periodicidade = "Mensal", Ativo = true,
    };

    [Fact]
    public async Task Listar_RetornaOkComLista()
    {
        var clienteId = Guid.NewGuid();
        _serviceMock.Setup(s => s.ListarPorClienteAsync(clienteId, _usuarioId, "admin"))
            .ReturnsAsync(new List<ContaRecorrenteDto> { CriarDto(clienteId) });

        var result = await _sut.Listar(clienteId);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponse<List<ContaRecorrenteDto>>>(ok.Value);
        Assert.Single(body.Dados!);
    }

    [Fact]
    public async Task Criar_RetornaCreated()
    {
        var clienteId = Guid.NewGuid();
        var input = new CriarContaRecorrenteDto
        {
            ClienteId = clienteId, Descricao = "Aluguel", Valor = 1000m,
            Tipo = "Pagar", DataInicio = new DateOnly(2026, 1, 1),
        };
        _serviceMock.Setup(s => s.CriarAsync(It.IsAny<CriarContaRecorrenteDto>(), _usuarioId, "admin"))
            .ReturnsAsync(CriarDto(clienteId));

        var result = await _sut.Criar(input);

        Assert.IsType<CreatedAtActionResult>(result);
    }

    [Fact]
    public async Task Atualizar_RetornaOk()
    {
        var clienteId = Guid.NewGuid();
        var id = Guid.NewGuid();
        _serviceMock.Setup(s => s.AtualizarAsync(clienteId, id, It.IsAny<AtualizarContaRecorrenteDto>(), _usuarioId, "admin"))
            .ReturnsAsync(CriarDto(clienteId));

        var result = await _sut.Atualizar(clienteId, id, new AtualizarContaRecorrenteDto());

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<ApiResponse<ContaRecorrenteDto>>(ok.Value);
    }

    [Fact]
    public async Task Desativar_RetornaOk()
    {
        var clienteId = Guid.NewGuid();
        var id = Guid.NewGuid();
        _serviceMock.Setup(s => s.DesativarAsync(clienteId, id, _usuarioId, "admin")).Returns(Task.CompletedTask);

        var result = await _sut.Desativar(clienteId, id);

        Assert.IsType<OkObjectResult>(result);
        _serviceMock.Verify(s => s.DesativarAsync(clienteId, id, _usuarioId, "admin"), Times.Once);
    }
}
