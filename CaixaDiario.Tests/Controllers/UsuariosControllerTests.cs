using System.Security.Claims;
using CaixaDiario.API.Controllers;
using CaixaDiario.API.DTOs.Usuarios;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CaixaDiario.Tests.Controllers;

public class UsuariosControllerTests
{
    private readonly Mock<IUsuarioService> _serviceMock = new();

    private UsuariosController CriarController(string perfil)
    {
        var claims = new[]
        {
            new Claim("perfil", perfil),
            new Claim("nome_usuario", "adminuser")
        };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        var controller = new UsuariosController(_serviceMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return controller;
    }

    private static UsuarioDto CriarUsuarioDto() => new()
    {
        Id = Guid.NewGuid(),
        NomeUsuario = "cli1",
        Nome = "Cliente Um",
        Perfil = "cliente",
        Ativo = true,
        CriadoEm = DateTime.UtcNow
    };

    [Fact]
    public async Task Listar_Admin_RetornaOkComLista()
    {
        var sut = CriarController("admin");
        var lista = new List<UsuarioDto> { CriarUsuarioDto() };
        _serviceMock.Setup(s => s.ListarAsync()).ReturnsAsync(lista);

        var result = await sut.Listar();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponse<List<UsuarioDto>>>(ok.Value);
        Assert.Single(body.Dados!);
    }

    [Fact]
    public async Task Listar_NaoAdmin_LancaSemPermissao()
    {
        var sut = CriarController("cliente");

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.Listar());

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.SEM_PERMISSAO, ex.Codigo);
    }

    [Fact]
    public async Task ObterPorId_Admin_RetornaOk()
    {
        var sut = CriarController("admin");
        var dto = CriarUsuarioDto();
        _serviceMock.Setup(s => s.ObterPorIdAsync(dto.Id)).ReturnsAsync(dto);

        var result = await sut.ObterPorId(dto.Id);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponse<UsuarioDto>>(ok.Value);
        Assert.Equal(dto.Id, body.Dados!.Id);
    }

    [Fact]
    public async Task Criar_Admin_RetornaCreated201()
    {
        var sut = CriarController("admin");
        var dto = CriarUsuarioDto();
        var criarDto = new CriarUsuarioDto { NomeUsuario = "cli1", Nome = "Cliente Um", Senha = "1234" };
        _serviceMock.Setup(s => s.CriarAsync(criarDto, "adminuser")).ReturnsAsync(dto);

        var result = await sut.Criar(criarDto);

        Assert.IsType<CreatedAtActionResult>(result);
    }

    [Fact]
    public async Task Atualizar_Admin_RetornaOk()
    {
        var sut = CriarController("admin");
        var id = Guid.NewGuid();
        var dto = CriarUsuarioDto();
        var atualizarDto = new AtualizarUsuarioDto { NomeUsuario = "cli1", Nome = "Cliente Um" };
        _serviceMock.Setup(s => s.AtualizarAsync(id, atualizarDto, "adminuser")).ReturnsAsync(dto);

        var result = await sut.Atualizar(id, atualizarDto);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Desativar_Admin_RetornaOk()
    {
        var sut = CriarController("admin");
        var id = Guid.NewGuid();
        _serviceMock.Setup(s => s.DesativarAsync(id, "adminuser")).Returns(Task.CompletedTask);

        var result = await sut.Desativar(id);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Desativar_NaoAdmin_LancaSemPermissao()
    {
        var sut = CriarController("cliente");

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.Desativar(Guid.NewGuid()));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Listar_SemClaimPerfil_LancaSemPermissao()
    {
        // Create controller with no claims at all (FindFirst returns null)
        var controller = new UsuariosController(_serviceMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal() }
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() => controller.Listar());

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.SEM_PERMISSAO, ex.Codigo);
    }

    [Fact]
    public async Task ObterPorId_NaoAdmin_LancaSemPermissao()
    {
        var sut = CriarController("cliente");

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.ObterPorId(Guid.NewGuid()));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.SEM_PERMISSAO, ex.Codigo);
    }

    [Fact]
    public async Task Criar_NaoAdmin_LancaSemPermissao()
    {
        var sut = CriarController("cliente");

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.Criar(new CriarUsuarioDto { NomeUsuario = "x", Nome = "y", Senha = "1234" }));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.SEM_PERMISSAO, ex.Codigo);
    }

    [Fact]
    public async Task Atualizar_NaoAdmin_LancaSemPermissao()
    {
        var sut = CriarController("cliente");

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.Atualizar(Guid.NewGuid(), new AtualizarUsuarioDto { NomeUsuario = "x", Nome = "y" }));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.SEM_PERMISSAO, ex.Codigo);
    }
}
