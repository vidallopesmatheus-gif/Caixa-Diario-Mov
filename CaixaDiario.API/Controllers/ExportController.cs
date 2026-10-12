using ClosedXML.Excel;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/export")]
[Authorize]
public class ExportController : ControllerBase
{
    private readonly IRegistroRepository _registroRepository;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly ICategoriaRepository _categoriaRepository;
    private readonly IMetricasService _metricasService;
    private readonly IFinanciamentoService _financiamentoService;

    public ExportController(
        IRegistroRepository registroRepository, IUsuarioRepository usuarioRepository,
        ICategoriaRepository categoriaRepository, IMetricasService metricasService,
        IFinanciamentoService financiamentoService)
    {
        _registroRepository = registroRepository;
        _usuarioRepository = usuarioRepository;
        _categoriaRepository = categoriaRepository;
        _metricasService = metricasService;
        _financiamentoService = financiamentoService;
    }

    private Guid ObterUsuarioId() => Guid.Parse(User.FindFirst("id")!.Value);
    private string ObterPerfil() => User.FindFirst("perfil")!.Value;

    private async Task<List<CaixaDiario.API.Models.RegistroDiario>> CarregarEValidar(Guid clienteId, DateOnly de, DateOnly ate)
    {
        if (ObterPerfil() == "cliente" && ObterUsuarioId() != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        if (ate < de)
            throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Data final deve ser maior ou igual à inicial.");

        return await _registroRepository.ListarPorPeriodoAsync(clienteId, de, ate);
    }

    // Rota original mantida para compatibilidade
    [HttpGet("{clienteId:guid}")]
    public async Task<IActionResult> Exportar(Guid clienteId, [FromQuery] DateOnly de, [FromQuery] DateOnly ate)
        => await ExportarXlsx(clienteId, de, ate);

    [HttpGet("{clienteId:guid}/xlsx")]
    public async Task<IActionResult> ExportarXlsx(Guid clienteId, [FromQuery] DateOnly de, [FromQuery] DateOnly ate)
    {
        var registros = await CarregarEValidar(clienteId, de, ate);

        using var workbook = new XLWorkbook();

        // Aba 1: Resumo Diário
        var ws1 = workbook.Worksheets.Add("Resumo Diário");
        ws1.Cell(1, 1).Value = "Data"; ws1.Cell(1, 2).Value = "Total Entradas (R$)";
        ws1.Cell(1, 3).Value = "Total Saídas (R$)"; ws1.Cell(1, 4).Value = "Lucro Operacional (R$)";
        ws1.Cell(1, 5).Value = "Saldo Final (R$)";
        var h1 = ws1.Row(1); h1.Style.Font.Bold = true;
        h1.Style.Fill.BackgroundColor = XLColor.FromHtml("#2C2C2E"); h1.Style.Font.FontColor = XLColor.White;

        int row = 2;
        foreach (var r in registros)
        {
            // Transferências entre contas e rendimento de investimento não são receita/despesa — ficam
            // de fora dos totais do relatório (a listagem "Por Categoria" abaixo continua trazendo cada
            // lançamento individualmente, incluindo esses, como registro bruto do que aconteceu).
            var te = r.Entradas.Where(e => LancamentoFiltro.EhOperacional(e.TipoCusto)).Sum(e => e.Valor);
            var ts = r.Saidas.Where(s => LancamentoFiltro.EhOperacional(s.TipoCusto)).Sum(s => s.Valor);
            ws1.Cell(row, 1).Value = r.Data.ToString("dd/MM/yyyy");
            ws1.Cell(row, 2).Value = (double)te; ws1.Cell(row, 3).Value = (double)ts;
            ws1.Cell(row, 4).Value = (double)(te - ts); ws1.Cell(row, 5).Value = (double)r.SaldoFinal;
            row++;
        }
        ws1.Columns().AdjustToContents();

        // Aba 2: Por Categoria
        var ws2 = workbook.Worksheets.Add("Por Categoria");
        ws2.Cell(1, 1).Value = "Data"; ws2.Cell(1, 2).Value = "Tipo";
        ws2.Cell(1, 3).Value = "Categoria"; ws2.Cell(1, 4).Value = "Descrição"; ws2.Cell(1, 5).Value = "Valor (R$)";
        var h2 = ws2.Row(1); h2.Style.Font.Bold = true;
        h2.Style.Fill.BackgroundColor = XLColor.FromHtml("#2C2C2E"); h2.Style.Font.FontColor = XLColor.White;

        int row2 = 2;
        foreach (var r in registros)
        {
            foreach (var e in r.Entradas)
            {
                ws2.Cell(row2, 1).Value = r.Data.ToString("dd/MM/yyyy"); ws2.Cell(row2, 2).Value = "Entrada";
                ws2.Cell(row2, 3).Value = e.Categoria ?? ""; ws2.Cell(row2, 4).Value = e.Descricao;
                ws2.Cell(row2, 5).Value = (double)e.Valor; row2++;
            }
            foreach (var s in r.Saidas)
            {
                ws2.Cell(row2, 1).Value = r.Data.ToString("dd/MM/yyyy"); ws2.Cell(row2, 2).Value = "Saída";
                ws2.Cell(row2, 3).Value = s.Categoria ?? ""; ws2.Cell(row2, 4).Value = s.Descricao;
                ws2.Cell(row2, 5).Value = (double)s.Valor; row2++;
            }
        }
        ws2.Columns().AdjustToContents();

        // Aba 3: Métricas resumidas
        var ws3 = workbook.Worksheets.Add("Métricas");
        ws3.Cell(1, 1).Value = "Métrica"; ws3.Cell(1, 2).Value = "Valor";
        ws3.Row(1).Style.Font.Bold = true;
        var totalEnt = registros.Sum(r => r.Entradas.Where(e => LancamentoFiltro.EhOperacional(e.TipoCusto)).Sum(e => e.Valor));
        var totalSai = registros.Sum(r => r.Saidas.Where(s => LancamentoFiltro.EhOperacional(s.TipoCusto)).Sum(s => s.Valor));
        ws3.Cell(2, 1).Value = "Total Entradas"; ws3.Cell(2, 2).Value = (double)totalEnt;
        ws3.Cell(3, 1).Value = "Total Saídas"; ws3.Cell(3, 2).Value = (double)totalSai;
        ws3.Cell(4, 1).Value = "Lucro Operacional"; ws3.Cell(4, 2).Value = (double)(totalEnt - totalSai);
        ws3.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"relatorio_{de:yyyy-MM-dd}_a_{ate:yyyy-MM-dd}.xlsx");
    }

    [HttpGet("{clienteId:guid}/csv")]
    public async Task<IActionResult> ExportarCsv(Guid clienteId, [FromQuery] DateOnly de, [FromQuery] DateOnly ate)
    {
        var registros = await CarregarEValidar(clienteId, de, ate);

        var sb = new StringBuilder();
        sb.AppendLine("data,tipo,categoria,tipoCusto,descricao,valor");

        foreach (var r in registros)
        {
            foreach (var e in r.Entradas)
                sb.AppendLine($"{r.Data:yyyy-MM-dd},entrada,{e.Categoria ?? ""},{ e.TipoCusto ?? ""},\"{e.Descricao.Replace("\"", "\"\"")}\",{e.Valor:F2}");
            foreach (var s in r.Saidas)
                sb.AppendLine($"{r.Data:yyyy-MM-dd},saida,{s.Categoria ?? ""},{s.TipoCusto ?? ""},\"{s.Descricao.Replace("\"", "\"\"")}\",{s.Valor:F2}");
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"relatorio_{de:yyyy-MM-dd}_a_{ate:yyyy-MM-dd}.csv");
    }

    [HttpGet("{clienteId:guid}/pdf")]
    public async Task<IActionResult> ExportarPdf(Guid clienteId, [FromQuery] DateOnly de, [FromQuery] DateOnly ate)
    {
        var registros = await CarregarEValidar(clienteId, de, ate);

        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Text($"Relatório Financeiro — {de:dd/MM/yyyy} a {ate:dd/MM/yyyy}")
                    .SemiBold().FontSize(14).FontColor(Colors.Grey.Darken3);

                page.Content().Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Text("Resumo Diário").Bold().FontSize(11);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(70); c.RelativeColumn(); c.RelativeColumn();
                            c.RelativeColumn(); c.RelativeColumn();
                        });

                        table.Header(h =>
                        {
                            h.Cell().Text("Data").Bold();
                            h.Cell().Text("Entradas").Bold();
                            h.Cell().Text("Saídas").Bold();
                            h.Cell().Text("Lucro Op.").Bold();
                            h.Cell().Text("Saldo Final").Bold();
                        });

                        foreach (var r in registros)
                        {
                            var te = r.Entradas.Where(e => LancamentoFiltro.EhOperacional(e.TipoCusto)).Sum(e => e.Valor);
                            var ts = r.Saidas.Where(s => LancamentoFiltro.EhOperacional(s.TipoCusto)).Sum(s => s.Valor);
                            table.Cell().Text(r.Data.ToString("dd/MM/yyyy"));
                            table.Cell().Text($"R$ {te:N2}");
                            table.Cell().Text($"R$ {ts:N2}");
                            table.Cell().Text($"R$ {te - ts:N2}");
                            table.Cell().Text($"R$ {r.SaldoFinal:N2}");
                        }
                    });

                    var totalE = registros.Sum(r => r.Entradas.Where(e => LancamentoFiltro.EhOperacional(e.TipoCusto)).Sum(e => e.Valor));
                    var totalS = registros.Sum(r => r.Saidas.Where(s => LancamentoFiltro.EhOperacional(s.TipoCusto)).Sum(s => s.Valor));
                    col.Item().Text($"Total Entradas: R$ {totalE:N2} | Total Saídas: R$ {totalS:N2} | Lucro: R$ {totalE - totalS:N2}").Bold();
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Página "); x.CurrentPageNumber(); x.Span(" de "); x.TotalPages();
                });
            });
        });

        var pdfBytes = document.GeneratePdf();
        return File(pdfBytes, "application/pdf", $"relatorio_{de:yyyy-MM-dd}_a_{ate:yyyy-MM-dd}.pdf");
    }

    private static readonly Dictionary<string, string> BlocoLabel = new()
    {
        ["RECEITAS OPERACIONAIS"] = "Receitas Operacionais",
        ["DEDUÇÕES DA RECEITA"] = "Deduções da Receita",
        ["CUSTOS OPERACIONAIS"] = "Custos Operacionais",
        ["DESPESAS OPERACIONAIS"] = "Despesas Operacionais",
        ["ATIVIDADES DE INVESTIMENTO"] = "Atividades de Investimento",
        ["ATIVIDADES DE FINANCIAMENTO"] = "Atividades de Financiamento",
    };

    private static readonly Color MvNavy = Color.FromHex("#0B2545");
    private static readonly Color MvBlue = Color.FromHex("#1E6FD9");

    // Item E3 do bundle D6JWEAJa: PDF da DRE — sempre um único mês (bate com o padrão de nome do
    // arquivo AAAA-MM), nunca trimestre/ano, mesmo que a tela esteja comparando outros períodos.
    [HttpGet("{clienteId:guid}/dre-pdf")]
    public async Task<IActionResult> ExportarDrePdf(Guid clienteId, [FromQuery] int ano, [FromQuery] int mes, [FromQuery] Guid? contaBancariaId = null)
    {
        if (ObterPerfil() == "cliente" && ObterUsuarioId() != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        var cliente = await _usuarioRepository.ObterPorIdAsync(clienteId)
            ?? throw new ApiException(404, CodigoRetorno.USUARIO_NAO_ENCONTRADO, "Cliente não encontrado.");

        var de = new DateOnly(ano, mes, 1);
        var ate = new DateOnly(ano, mes, DateTime.DaysInMonth(ano, mes));
        var refAnterior = de.AddMonths(-1);
        var deAnterior = new DateOnly(refAnterior.Year, refAnterior.Month, 1);
        var ateAnterior = new DateOnly(refAnterior.Year, refAnterior.Month, DateTime.DaysInMonth(refAnterior.Year, refAnterior.Month));

        var categorias = await _categoriaRepository.ListarTodasPorClienteAsync(clienteId);
        var todosRegistros = await _registroRepository.ListarPorPeriodoAsync(clienteId, deAnterior, ate);
        if (contaBancariaId.HasValue)
            todosRegistros = todosRegistros.Where(r => r.ContaBancariaId == contaBancariaId.Value).ToList();

        var registrosMes = todosRegistros.Where(r => r.Data >= de && r.Data <= ate).ToList();
        var registrosMesAnterior = todosRegistros.Where(r => r.Data >= deAnterior && r.Data <= ateAnterior).ToList();

        var dre = _metricasService.CalcularDre(registrosMes, categorias);
        var dreAnterior = _metricasService.CalcularDre(registrosMesAnterior, categorias);
        var pontoEquilibrio = _metricasService.CalcularPontoEquilibrio(
            dre.ReceitaBruta, dre.MargemContribuicao, Math.Abs(dre.DespesasFixas.Total), ate);

        var financiamentos = await _financiamentoService.ListarAsync(clienteId, ObterUsuarioId(), ObterPerfil());
        var financiamentosAtivos = financiamentos.Where(f => f.Status == "Ativo").ToList();
        var saldoDevedorTotal = financiamentosAtivos.Sum(f => f.SaldoDevedor);
        var parcelaMensalComprometida = financiamentosAtivos.Sum(f => f.ProximaParcelaValor ?? 0);

        decimal? variacaoReceita = dreAnterior.ReceitaBruta > 0
            ? Math.Round((dre.ReceitaBruta - dreAnterior.ReceitaBruta) / dreAnterior.ReceitaBruta * 100, 1)
            : null;
        decimal? variacaoResultado = dreAnterior.ResultadoLiquido != 0
            ? Math.Round((dre.ResultadoLiquido - dreAnterior.ResultadoLiquido) / Math.Abs(dreAnterior.ResultadoLiquido) * 100, 1)
            : null;

        QuestPDF.Settings.License = LicenseType.Community;
        var nomeMes = de.ToString("MMMM/yyyy", new System.Globalization.CultureInfo("pt-BR"));

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Background(MvNavy).Padding(14).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("💰 Money Vision").FontColor(Colors.White).Bold().FontSize(16);
                        c.Item().Text("Demonstrativo de Resultado (DRE)").FontColor(Colors.White).FontSize(10);
                    });
                    row.ConstantItem(180).Column(c =>
                    {
                        c.Item().AlignRight().Text(cliente.Nome).FontColor(Colors.White).Bold().FontSize(11);
                        c.Item().AlignRight().Text(nomeMes).FontColor(Colors.White).FontSize(10);
                    });
                });

                page.Content().PaddingTop(14).Column(col =>
                {
                    col.Spacing(12);

                    col.Item().Text("Demonstrativo por bloco").Bold().FontSize(11).FontColor(MvNavy);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(); c.RelativeColumn(); });
                        table.Header(h =>
                        {
                            h.Cell().Text("Bloco").Bold();
                            h.Cell().AlignRight().Text("Total").Bold();
                            h.Cell().AlignRight().Text("%").Bold();
                        });
                        foreach (var bloco in dre.Blocos)
                        {
                            table.Cell().Text(BlocoLabel.GetValueOrDefault(bloco.Bloco, bloco.Bloco));
                            table.Cell().AlignRight().Text($"R$ {bloco.Total:N2}");
                            table.Cell().AlignRight().Text(bloco.Percentual.HasValue ? $"{bloco.Percentual:N1}%" : "—");
                            foreach (var grupo in bloco.Grupos)
                            {
                                table.Cell().PaddingLeft(10).Text(grupo.Nome).FontColor(Colors.Grey.Darken1).FontSize(8);
                                table.Cell().AlignRight().Text($"R$ {grupo.Total:N2}").FontColor(Colors.Grey.Darken1).FontSize(8);
                                table.Cell().AlignRight().Text(grupo.Percentual.HasValue ? $"{grupo.Percentual:N1}%" : "—").FontColor(Colors.Grey.Darken1).FontSize(8);
                            }
                        }
                    });

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(t => { t.Span("Resultado Líquido: ").Bold(); t.Span($"R$ {dre.ResultadoLiquido:N2}"); });
                        row.RelativeItem().AlignRight().Text(t => { t.Span("Margem: ").Bold(); t.Span(dre.ResultadoLiquidoPercentual.HasValue ? $"{dre.ResultadoLiquidoPercentual:N1}%" : "—"); });
                    });

                    col.Item().Text("Indicadores").Bold().FontSize(11).FontColor(MvNavy);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                        void Linha(string label, string valor)
                        {
                            table.Cell().Text(label);
                            table.Cell().AlignRight().Text(valor).Bold();
                        }
                        Linha("Margem bruta (contribuição)", dre.MargemContribuicaoPercentual.HasValue ? $"{dre.MargemContribuicaoPercentual:N1}%" : "—");
                        Linha("Margem líquida", dre.ResultadoLiquidoPercentual.HasValue ? $"{dre.ResultadoLiquidoPercentual:N1}%" : "—");
                        Linha("Despesas fixas (% da receita)", dre.DespesasFixas.Percentual.HasValue ? $"{dre.DespesasFixas.Percentual:N1}%" : "—");
                        Linha("Ponto de equilíbrio (mensal)", pontoEquilibrio.Disponivel && pontoEquilibrio.ValorMensal.HasValue ? $"R$ {pontoEquilibrio.ValorMensal:N2}" : "Não disponível");
                    });

                    col.Item().Text($"Comparação com {refAnterior:MMMM/yyyy}").Bold().FontSize(11).FontColor(MvNavy);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                        table.Header(h =>
                        {
                            h.Cell().Text("");
                            h.Cell().AlignRight().Text(nomeMes).Bold();
                            h.Cell().AlignRight().Text(refAnterior.ToString("MMMM/yyyy", new System.Globalization.CultureInfo("pt-BR"))).Bold();
                            h.Cell().AlignRight().Text("Var.").Bold();
                        });
                        table.Cell().Text("Receita Bruta");
                        table.Cell().AlignRight().Text($"R$ {dre.ReceitaBruta:N2}");
                        table.Cell().AlignRight().Text($"R$ {dreAnterior.ReceitaBruta:N2}");
                        table.Cell().AlignRight().Text(variacaoReceita.HasValue ? $"{variacaoReceita:N1}%" : "—");
                        table.Cell().Text("Resultado Líquido");
                        table.Cell().AlignRight().Text($"R$ {dre.ResultadoLiquido:N2}");
                        table.Cell().AlignRight().Text($"R$ {dreAnterior.ResultadoLiquido:N2}");
                        table.Cell().AlignRight().Text(variacaoResultado.HasValue ? $"{variacaoResultado:N1}%" : "—");
                    });

                    col.Item().Text("💳 Dívidas").Bold().FontSize(11).FontColor(MvNavy);
                    if (financiamentosAtivos.Count == 0)
                    {
                        col.Item().Text("Nenhum financiamento/empréstimo/consórcio ativo.").FontColor(Colors.Grey.Darken1);
                    }
                    else
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                            table.Cell().Text("Saldo devedor total");
                            table.Cell().AlignRight().Text($"R$ {saldoDevedorTotal:N2}").Bold();
                            table.Cell().Text("Parcela mensal comprometida");
                            table.Cell().AlignRight().Text($"R$ {parcelaMensalComprometida:N2}").Bold();
                            table.Cell().Text("Contratos ativos");
                            table.Cell().AlignRight().Text($"{financiamentosAtivos.Count}");
                        });
                    }
                });

                page.Footer().Column(col =>
                {
                    col.Item().AlignCenter().Text($"Gerado em {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Darken1);
                    col.Item().AlignCenter().Text(x =>
                    {
                        x.DefaultTextStyle(y => y.FontSize(8).FontColor(Colors.Grey.Darken1));
                        x.Span("Página "); x.CurrentPageNumber(); x.Span(" de "); x.TotalPages();
                    });
                });
            });
        });

        var pdfBytes = document.GeneratePdf();
        var nomeArquivo = $"MoneyVision_DRE_{cliente.NomeUsuario}_{ano:D4}-{mes:D2}.pdf";
        return File(pdfBytes, "application/pdf", nomeArquivo);
    }
}
