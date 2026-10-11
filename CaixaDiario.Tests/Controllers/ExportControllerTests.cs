using System.Security.Claims;
using CaixaDiario.API.Controllers;
using CaixaDiario.API.DTOs.Financiamentos;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CaixaDiario.Tests.Controllers;

public class ExportControllerTests
{
    private readonly Mock<IRegistroRepository> _repoMock = new();
    private readonly Mock<IUsuarioRepository> _usuarioRepoMock = new();
    private readonly Mock<ICategoriaRepository> _categoriaRepoMock = new();
    private readonly Mock<IFinanciamentoService> _financiamentoServiceMock = new();

    private ExportController CriarSut(Guid usuarioId, string perfil)
    {
        _categoriaRepoMock.Setup(c => c.ListarTodasAsync()).ReturnsAsync(new List<Categoria>());
        _financiamentoServiceMock.Setup(f => f.ListarAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(new List<FinanciamentoDto>());
        var sut = new ExportController(
            _repoMock.Object, _usuarioRepoMock.Object, _categoriaRepoMock.Object,
            new MetricasService(), _financiamentoServiceMock.Object);
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
                new ItemFinanceiroSaida { Descricao = "Aluguel", Valor = 50m, Categoria = "Despesas Administrativas", TipoCusto = "CustoFixo" },
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
    public async Task ExportarXlsx_ComTransferencia_NaoEntraNosTotaisDaAbaMetricas()
    {
        var clienteId = Guid.NewGuid();
        var registros = new List<RegistroDiario>
        {
            new()
            {
                Id = Guid.NewGuid(), ClienteId = clienteId, Data = new DateOnly(2026, 1, 5),
                Inicio = 100m, SaldoFinal = 1300m,
                Entradas = new()
                {
                    new ItemFinanceiro { Descricao = "Venda", Valor = 200m, Categoria = "Vendas", TipoCusto = "Receita" },
                    new ItemFinanceiro { Descricao = "Transferência recebida", Valor = 1000m, Categoria = "Transferência", TipoCusto = "Transferencia" },
                },
                Saidas = new(),
            },
        };
        SetupPeriodo(clienteId, registros);

        var result = await CriarSut(Guid.NewGuid(), "admin")
            .ExportarXlsx(clienteId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        var file = Assert.IsType<FileContentResult>(result);
        using var ms = new MemoryStream(file.FileContents);
        using var workbook = new XLWorkbook(ms);
        var wsMetricas = workbook.Worksheet("Métricas");
        Assert.Equal(200d, wsMetricas.Cell(2, 2).GetDouble()); // Total Entradas — só a venda, sem a transferência
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

    // ── ExportarDrePdf (item E3 do bundle D6JWEAJa) ────────────────────────────────────────────

    [Fact]
    public async Task ExportarDrePdf_RetornaArquivoPdfComNomeNoPadraoMoneyVision()
    {
        var clienteId = Guid.NewGuid();
        var cliente = new Usuario { Id = clienteId, Nome = "Padaria Dois Irmãos", NomeUsuario = "padaria_dois_irmaos" };
        _usuarioRepoMock.Setup(u => u.ObterPorIdAsync(clienteId)).ReturnsAsync(cliente);
        _repoMock.Setup(r => r.ListarPorPeriodoAsync(clienteId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(CriarRegistros(clienteId));

        var result = await CriarSut(Guid.NewGuid(), "admin").ExportarDrePdf(clienteId, 2026, 1, null);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.NotEmpty(file.FileContents);
        Assert.Equal("MoneyVision_DRE_padaria_dois_irmaos_2026-01.pdf", file.FileDownloadName);
    }

    [Fact]
    public async Task ExportarDrePdf_ClienteAcessandoOutro_LancaAcessoNegado()
    {
        var sut = CriarSut(Guid.NewGuid(), "cliente");
        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.ExportarDrePdf(Guid.NewGuid(), 2026, 1, null));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task ExportarDrePdf_ClienteInexistente_LancaUsuarioNaoEncontrado()
    {
        var clienteId = Guid.NewGuid();
        _usuarioRepoMock.Setup(u => u.ObterPorIdAsync(clienteId)).ReturnsAsync((Usuario?)null);

        var ex = await Assert.ThrowsAsync<ApiException>(() => CriarSut(clienteId, "cliente").ExportarDrePdf(clienteId, 2026, 1, null));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task ExportarDrePdf_SemFinanciamentoAtivo_NaoQuebraEGeraPdf()
    {
        var clienteId = Guid.NewGuid();
        _usuarioRepoMock.Setup(u => u.ObterPorIdAsync(clienteId)).ReturnsAsync(new Usuario { Id = clienteId, Nome = "Cliente", NomeUsuario = "cliente1" });
        _repoMock.Setup(r => r.ListarPorPeriodoAsync(clienteId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(new List<RegistroDiario>());

        var result = await CriarSut(clienteId, "cliente").ExportarDrePdf(clienteId, 2026, 2, null);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.NotEmpty(file.FileContents);
    }
}
