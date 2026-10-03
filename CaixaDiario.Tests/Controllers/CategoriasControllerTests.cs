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

public class CategoriasControllerTests
{
    private readonly Mock<ICategoriaService> _serviceMock = new();

    private CategoriasController CriarController(string? perfil)
    {
        var controller = new CategoriasController(_serviceMock.Object);
        if (perfil == null) return controller;

        var claims = new[] { new Claim("perfil", perfil) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } };
        return controller;
    }

    [Fact]
    public async Task Listar_RetornaOkComCategoriasAgrupadas()
    {
        // Leitura é global a qualquer usuário autenticado (formulários de lançamento do cliente
        // dependem disso) — sem ControllerContext/perfil nenhum, não deve exigir admin.
        var sut = CriarController(null);
        _serviceMock.Setup(s => s.ListarAgrupadasAsync())
            .ReturnsAsync(new CategoriasAgrupadasDto
            {
                Entradas = new() { new CategoriaItemDto { Nome = "Vendas", TipoCusto = "Receita" } },
                Saidas = new(),
            });

        var result = await sut.Listar();

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<CategoriasAgrupadasDto>(ok.Value);
        Assert.Single(dto.Entradas);
    }

    [Fact]
    public async Task Criar_NaoAdmin_LancaSemPermissao()
    {
        var sut = CriarController("cliente");

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.Criar(new CriarCategoriaDto { Nome = "X", GrupoId = Guid.NewGuid() }));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.SEM_PERMISSAO, ex.Codigo);
        _serviceMock.Verify(s => s.CriarAsync(It.IsAny<CriarCategoriaDto>()), Times.Never);
    }

    [Fact]
    public async Task Criar_Admin_RetornaOk()
    {
        var sut = CriarController("admin");
        var dto = new CriarCategoriaDto { Nome = "X", GrupoId = Guid.NewGuid() };
        _serviceMock.Setup(s => s.CriarAsync(dto)).ReturnsAsync(new CategoriaDto { Id = Guid.NewGuid(), Nome = "X" });

        var result = await sut.Criar(dto);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Excluir_NaoAdmin_LancaSemPermissao()
    {
        var sut = CriarController("cliente");

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.Excluir(Guid.NewGuid()));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.SEM_PERMISSAO, ex.Codigo);
        _serviceMock.Verify(s => s.ExcluirOuInformarUsoAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Excluir_AdminQuandoEmUso_RetornaConflict()
    {
        var sut = CriarController("admin");
        var id = Guid.NewGuid();
        _serviceMock.Setup(s => s.ExcluirOuInformarUsoAsync(id))
            .ReturnsAsync(new ExclusaoCategoriaResultDto { Excluida = false, QuantidadeLancamentos = 3 });

        var result = await sut.Excluir(id);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var resposta = Assert.IsType<ApiResponse<ExclusaoCategoriaResultDto>>(conflict.Value);
        Assert.Equal(3, resposta.Dados!.QuantidadeLancamentos);
    }

    [Fact]
    public async Task Excluir_AdminQuandoSemUso_RetornaOk()
    {
        var sut = CriarController("admin");
        var id = Guid.NewGuid();
        _serviceMock.Setup(s => s.ExcluirOuInformarUsoAsync(id))
            .ReturnsAsync(new ExclusaoCategoriaResultDto { Excluida = true, QuantidadeLancamentos = 0 });

        var result = await sut.Excluir(id);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Atualizar_NaoAdmin_LancaSemPermissao()
    {
        var sut = CriarController("cliente");

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => sut.Atualizar(Guid.NewGuid(), new AtualizarCategoriaDto { Nome = "Y", GrupoId = Guid.NewGuid(), Ativa = true }));

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

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.Reordenar(new ReordenarCategoriasDto { Ids = new() }));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.SEM_PERMISSAO, ex.Codigo);
    }

    [Fact]
    public async Task Migrar_NaoAdmin_LancaSemPermissao()
    {
        var sut = CriarController("cliente");

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => sut.Migrar(Guid.NewGuid(), new MigrarCategoriaDto { ParaCategoriaId = Guid.NewGuid() }));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal(CodigoRetorno.SEM_PERMISSAO, ex.Codigo);
    }
}
