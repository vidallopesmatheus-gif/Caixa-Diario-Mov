using System.Security.Claims;
using CaixaDiario.API.Controllers;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CaixaDiario.Tests.Controllers;

public class ExportControllerTests
{
    private readonly Mock<IRegistroRepository> _repoMock = new();

    private ExportController CriarSut(Guid usuarioId, string perfil)
    {
        var sut = new ExportController(_repoMock.Object);
        var claims = new[] { new Claim("id", usuarioId.ToString()), new Claim("perfil", perfil) };
        sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims)) }
        };
        return sut;
    }

    private static List<RegistroDiario> CriarRegistros(Guid clienteId) => new()
    {
        new RegistroDiario
        {
            Id = Guid.NewGuid(), ClienteId = clienteId, Data = new DateOnly(2026, 1, 5),
            Inicio = 100m, SaldoFinal = 250m,
            Entradas = new()
            {
                new ItemFinanceiro { Descricao = "Venda \"especial\"", Valor = 200m, Categoria = "Vendas", TipoCusto = "Receita" },
            },
            Saidas = new()
            {
                new ItemFinanceiro { Descricao = "Aluguel", Valor = 50m, Categoria = "Despesas Administrativas", TipoCusto = "CustoFixo" },
            },
        },
    };

    private void SetupPeriodo(Guid clienteId, List<RegistroDiario> registros) =>
        _repoMock.Setup(r => r.ListarPorPeriodoAsync(clienteId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(registros);

    [Fact]
    public async Task ExportarXlsx_RetornaArquivoXlsx()
    {
        var clienteId = Guid.NewGuid();
        SetupPeriodo(clienteId, CriarRegistros(clienteId));

        var result = await CriarSut(Guid.NewGuid(), "admin")
            .ExportarXlsx(clienteId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.ContentType);
        Assert.NotEmpty(file.FileContents);
    }

    [Fact]
    public async Task Exportar_AliasRetornaXlsx()
    {
        var clienteId = Guid.NewGuid();
        SetupPeriodo(clienteId, CriarRegistros(clienteId));

        var result = await CriarSut(Guid.NewGuid(), "admin")
            .Exportar(clienteId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.ContentType);
    }

    [Fact]
    public async Task ExportarCsv_RetornaArquivoCsv()
    {
        var clienteId = Guid.NewGuid();
        SetupPeriodo(clienteId, CriarRegistros(clienteId));

        var result = await CriarSut(clienteId, "cliente")
            .ExportarCsv(clienteId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv", file.ContentType);
        Assert.NotEmpty(file.FileContents);
    }

    [Fact]
    public async Task ExportarPdf_RetornaArquivoPdf()
    {
        var clienteId = Guid.NewGuid();
        SetupPeriodo(clienteId, CriarRegistros(clienteId));

        var result = await CriarSut(Guid.NewGuid(), "admin")
            .ExportarPdf(clienteId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.NotEmpty(file.FileContents);
    }

    [Fact]
    public async Task ExportarCsv_ClienteAcessandoOutro_LancaAcessoNegado()
    {
        var sut = CriarSut(Guid.NewGuid(), "cliente");
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.ExportarCsv(Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task ExportarCsv_DataFinalMenorQueInicial_LancaDadosInvalidos()
    {
        var clienteId = Guid.NewGuid();
        var sut = CriarSut(clienteId, "cliente");
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.ExportarCsv(clienteId, new DateOnly(2026, 1, 31), new DateOnly(2026, 1, 1)));
        Assert.Equal(400, ex.StatusCode);
    }
}
