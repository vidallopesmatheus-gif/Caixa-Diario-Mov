using System.Security.Claims;
using CaixaDiario.API.Controllers;
using CaixaDiario.API.DTOs.Metas;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CaixaDiario.Tests.Controllers;

public class MetasControllerTests
{
    private readonly Mock<IMetaService> _serviceMock = new();
    private readonly MetasController _sut;
    private readonly Guid _usuarioId = Guid.NewGuid();

    public MetasControllerTests()
    {
        _sut = new MetasController(_serviceMock.Object);
        var claims = new[] { new Claim("id", _usuarioId.ToString()), new Claim("perfil", "admin"), new Claim("nome_usuario", "admin") };
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims)) }
        };
    }

    [Fact]
    public async Task Obter_RetornaOkComMeta()
    {
        var clienteId = Guid.NewGuid();
        var dto = new MetaAnualDto { Id = Guid.NewGuid(), ClienteId = clienteId, Ano = 2026, MetaReceita = 120000m, MetaLucro = 60000m };
        _serviceMock.Setup(s => s.ObterMetaAsync(clienteId, 2026, _usuarioId, "admin")).ReturnsAsync(dto);

        var result = await _sut.Obter(clienteId, 2026);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponse<MetaAnualDto>>(ok.Value);
        Assert.Equal(120000m, body.Dados!.MetaReceita);
    }

    [Fact]
    public async Task Salvar_RetornaOkComMeta()
    {
        var clienteId = Guid.NewGuid();
        var input = new SalvarMetaAnualDto { ClienteId = clienteId, Ano = 2026, MetaReceita = 120000m, MetaLucro = 60000m };
        var dto = new MetaAnualDto { Id = Guid.NewGuid(), ClienteId = clienteId, Ano = 2026, MetaReceita = 120000m, MetaLucro = 60000m };
        _serviceMock.Setup(s => s.SalvarMetaAsync(It.IsAny<SalvarMetaAnualDto>(), _usuarioId, "admin")).ReturnsAsync(dto);

        var result = await _sut.Salvar(input);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponse<MetaAnualDto>>(ok.Value);
        Assert.Equal(2026, body.Dados!.Ano);
    }

    [Fact]
    public async Task Excluir_ChamaServicoERetornaOk()
    {
        var metaId = Guid.NewGuid();
        _serviceMock.Setup(s => s.ExcluirMetaAsync(metaId, _usuarioId, "admin")).Returns(Task.CompletedTask);

        var result = await _sut.Excluir(metaId);

        Assert.IsType<OkObjectResult>(result);
        _serviceMock.Verify(s => s.ExcluirMetaAsync(metaId, _usuarioId, "admin"), Times.Once);
    }
}
