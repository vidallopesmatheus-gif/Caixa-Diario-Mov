using System.Security.Claims;
using CaixaDiario.API.Controllers;
using CaixaDiario.API.DTOs.Categorias;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CaixaDiario.Tests.Controllers;

public class CategoriasControllerTests
{
    private readonly Mock<ICategoriaService> _serviceMock = new();
    private readonly CategoriasController _sut;
    private readonly Guid _clienteId = Guid.NewGuid();

    public CategoriasControllerTests()
    {
        _sut = new CategoriasController(_serviceMock.Object);
        var claims = new[] { new Claim("id", _clienteId.ToString()), new Claim("perfil", "cliente") };
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims)) },
        };
    }

    [Fact]
    public async Task Listar_RetornaOkComCategoriasAgrupadas()
    {
        _serviceMock.Setup(s => s.ListarAgrupadasAsync(_clienteId))
            .ReturnsAsync(new CategoriasAgrupadasDto
            {
                Entradas = new() { new CategoriaItemDto { Nome = "Vendas", TipoCusto = "Receita" } },
                Saidas = new(),
            });

        var result = await _sut.Listar(_clienteId);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<CategoriasAgrupadasDto>(ok.Value);
        Assert.Single(dto.Entradas);
    }

    [Fact]
    public async Task Excluir_QuandoEmUso_RetornaConflict()
    {
        var id = Guid.NewGuid();
        _serviceMock.Setup(s => s.ExcluirOuInformarUsoAsync(_clienteId, id, _clienteId, "cliente"))
            .ReturnsAsync(new ExclusaoCategoriaResultDto { Excluida = false, QuantidadeLancamentos = 3 });

        var result = await _sut.Excluir(_clienteId, id);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var resposta = Assert.IsType<ApiResponse<ExclusaoCategoriaResultDto>>(conflict.Value);
        Assert.Equal(3, resposta.Dados!.QuantidadeLancamentos);
    }

    [Fact]
    public async Task Excluir_QuandoSemUso_RetornaOk()
    {
        var id = Guid.NewGuid();
        _serviceMock.Setup(s => s.ExcluirOuInformarUsoAsync(_clienteId, id, _clienteId, "cliente"))
            .ReturnsAsync(new ExclusaoCategoriaResultDto { Excluida = true, QuantidadeLancamentos = 0 });

        var result = await _sut.Excluir(_clienteId, id);

        Assert.IsType<OkObjectResult>(result);
    }
}
