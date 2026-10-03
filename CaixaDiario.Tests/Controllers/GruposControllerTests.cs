using System.Security.Claims;
using CaixaDiario.API.Controllers;
using CaixaDiario.API.DTOs.Categorias;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CaixaDiario.Tests.Controllers;

public class GruposControllerTests
{
    private readonly Mock<IGrupoService> _serviceMock = new();

    private GruposController CriarController(string? perfil)
    {
        var controller = new GruposController(_serviceMock.Object);
        if (perfil == null) return controller;

        var claims = new[] { new Claim("perfil", perfil) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } };
        return controller;
    }

    [Fact]
    public async Task Listar_RetornaOkComGrupos()
    {
        // Leitura é global a qualquer usuário autenticado — sem perfil nenhum no contexto, não
        // deve exigir admin.
        var sut = CriarController(null);
        _serviceMock.Setup(s => s.ListarTodosAsync()).ReturnsAsync(new List<GrupoDto> { new() { Id = Guid.NewGuid(), Nome = "Vendas" } });

        var result = await sut.Listar();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponse<List<GrupoDto>>>(ok.Value);
        Assert.Single(body.Dados!);
    }

    [Fact]
    public async Task ListarBlocos_RetornaOk()
    {
        var sut = CriarController(null);
        _serviceMock.Setup(s => s.ListarBlocosAsync()).ReturnsAsync(new[] { "RECEITAS OPERACIONAIS" });

        var result = await sut.ListarBlocos();

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Criar_NaoAdmin_LancaSemPermissao()
    {
        var sut = CriarController("cliente");

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => sut.Criar(new CriarGrupoDto { Nome = "Novo", Bloco = "DESPESAS OPERACIONAIS" }));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.SEM_PERMISSAO, ex.Codigo);
        _serviceMock.Verify(s => s.CriarAsync(It.IsAny<CriarGrupoDto>()), Times.Never);
    }

    [Fact]
    public async Task Criar_Admin_RetornaOk()
    {
        var sut = CriarController("admin");
        var dto = new CriarGrupoDto { Nome = "Novo", Bloco = "DESPESAS OPERACIONAIS" };
        _serviceMock.Setup(s => s.CriarAsync(dto)).ReturnsAsync(new GrupoDto { Id = Guid.NewGuid(), Nome = "Novo" });

        var result = await sut.Criar(dto);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Atualizar_NaoAdmin_LancaSemPermissao()
    {
        var sut = CriarController("cliente");

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => sut.Atualizar(Guid.NewGuid(), new AtualizarGrupoDto { Nome = "Y", Bloco = "DESPESAS OPERACIONAIS" }));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.SEM_PERMISSAO, ex.Codigo);
    }

    [Fact]
    public async Task Desativar_NaoAdmin_LancaSemPermissao()
    {
        var sut = CriarController("cliente");

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.Desativar(Guid.NewGuid()));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.SEM_PERMISSAO, ex.Codigo);
    }

    [Fact]
    public async Task Reordenar_NaoAdmin_LancaSemPermissao()
    {
        var sut = CriarController("cliente");

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.Reordenar(new ReordenarGruposDto { Ids = new() }));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.SEM_PERMISSAO, ex.Codigo);
    }
}
