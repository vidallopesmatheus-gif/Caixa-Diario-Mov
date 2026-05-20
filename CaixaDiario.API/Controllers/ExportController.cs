using ClosedXML.Excel;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/export")]
[Authorize]
public class ExportController : ControllerBase
{
    private readonly IRegistroRepository _registroRepository;

    public ExportController(IRegistroRepository registroRepository) => _registroRepository = registroRepository;

    private Guid ObterUsuarioId() => Guid.Parse(User.FindFirst("id")!.Value);
    private string ObterPerfil() => User.FindFirst("perfil")!.Value;

    [HttpGet("{clienteId:guid}")]
    public async Task<IActionResult> Exportar(Guid clienteId, [FromQuery] DateOnly de, [FromQuery] DateOnly ate)
    {
        if (ObterPerfil() == "cliente" && ObterUsuarioId() != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        if (ate < de)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Data final deve ser maior ou igual à inicial.");

        var registros = await _registroRepository.ListarPorPeriodoAsync(clienteId, de, ate);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Relatório");

        ws.Cell(1, 1).Value = "Data";
        ws.Cell(1, 2).Value = "Total Entradas (R$)";
        ws.Cell(1, 3).Value = "Total Saídas (R$)";
        ws.Cell(1, 4).Value = "Lucro Operacional (R$)";
        ws.Cell(1, 5).Value = "Saldo Final (R$)";
        var headerRow = ws.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#2C2C2E");
        headerRow.Style.Font.FontColor = XLColor.White;

        int row = 2;
        foreach (var r in registros)
        {
            var totalEntradas = r.Entradas.Sum(e => e.Valor);
            var totalSaidas = r.Saidas.Sum(s => s.Valor);
            ws.Cell(row, 1).Value = r.Data.ToString("dd/MM/yyyy");
            ws.Cell(row, 2).Value = (double)totalEntradas;
            ws.Cell(row, 3).Value = (double)totalSaidas;
            ws.Cell(row, 4).Value = (double)(totalEntradas - totalSaidas);
            ws.Cell(row, 5).Value = (double)r.SaldoFinal;
            row++;
        }

        if (registros.Count > 0)
        {
            ws.Cell(row, 1).Value = "TOTAL";
            ws.Cell(row, 2).Value = (double)registros.Sum(r => r.Entradas.Sum(e => e.Valor));
            ws.Cell(row, 3).Value = (double)registros.Sum(r => r.Saidas.Sum(s => s.Valor));
            ws.Cell(row, 4).Value = (double)registros.Sum(r => r.Entradas.Sum(e => e.Valor) - r.Saidas.Sum(s => s.Valor));
            ws.Cell(row, 5).Value = (double)(registros.Last().SaldoFinal);
            ws.Row(row).Style.Font.Bold = true;
        }

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        var fileName = $"relatorio_{de:yyyy-MM-dd}_a_{ate:yyyy-MM-dd}.xlsx";
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
