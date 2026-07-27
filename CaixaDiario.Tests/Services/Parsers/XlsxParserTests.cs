using ClosedXML.Excel;
using CaixaDiario.API.Services.Parsers;

namespace CaixaDiario.Tests.Services.Parsers;

public class XlsxParserTests
{
    private static MemoryStream CriarPlanilha(Action<IXLWorksheet> preencher)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Extrato");
        preencher(ws);
        var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void Parse_ComColunaUnicaDeValorEDataComoDateTime_RetornaTransacoes()
    {
        var stream = CriarPlanilha(ws =>
        {
            ws.Cell(1, 1).Value = "Data"; ws.Cell(1, 2).Value = "Descricao"; ws.Cell(1, 3).Value = "Valor";
            ws.Cell(2, 1).Value = new DateTime(2026, 7, 1); ws.Cell(2, 2).Value = "Venda"; ws.Cell(2, 3).Value = 100;
            ws.Cell(3, 1).Value = new DateTime(2026, 7, 2); ws.Cell(3, 2).Value = "Compra"; ws.Cell(3, 3).Value = -50;
        });

        var resultado = XlsxParser.Parse(stream);

        Assert.Equal(2, resultado.Count);
        Assert.Equal("Entrada", resultado[0].Tipo);
        Assert.Equal(100m, resultado[0].Valor);
        Assert.Equal("Saida", resultado[1].Tipo);
        Assert.Equal(50m, resultado[1].Valor);
    }

    [Fact]
    public void Parse_ComDataComoTexto_ConverteViaColunaMapper()
    {
        var stream = CriarPlanilha(ws =>
        {
            ws.Cell(1, 1).Value = "Data"; ws.Cell(1, 2).Value = "Descricao"; ws.Cell(1, 3).Value = "Valor";
            ws.Cell(2, 1).Value = "01/07/2026"; ws.Cell(2, 2).Value = "Venda"; ws.Cell(2, 3).Value = "100,00";
        });

        var resultado = XlsxParser.Parse(stream);

        var t = Assert.Single(resultado);
        Assert.Equal(new DateOnly(2026, 7, 1), t.Data);
        Assert.Equal(100m, t.Valor);
    }

    [Fact]
    public void Parse_ComColunasSeparadasDeCreditoEDebito_ClassificaCorretamente()
    {
        var stream = CriarPlanilha(ws =>
        {
            ws.Cell(1, 1).Value = "Data"; ws.Cell(1, 2).Value = "Historico";
            ws.Cell(1, 3).Value = "Credito"; ws.Cell(1, 4).Value = "Debito";
            ws.Cell(2, 1).Value = new DateTime(2026, 7, 1); ws.Cell(2, 2).Value = "Recebimento"; ws.Cell(2, 3).Value = 300;
            ws.Cell(3, 1).Value = new DateTime(2026, 7, 2); ws.Cell(3, 2).Value = "Pagamento"; ws.Cell(3, 4).Value = 150;
        });

        var resultado = XlsxParser.Parse(stream);

        Assert.Equal(2, resultado.Count);
        Assert.Equal("Entrada", resultado[0].Tipo);
        Assert.Equal(300m, resultado[0].Valor);
        Assert.Equal("Saida", resultado[1].Tipo);
        Assert.Equal(150m, resultado[1].Valor);
    }

    [Fact]
    public void Parse_ComLinhaSemDescricao_Ignora()
    {
        var stream = CriarPlanilha(ws =>
        {
            ws.Cell(1, 1).Value = "Data"; ws.Cell(1, 2).Value = "Descricao"; ws.Cell(1, 3).Value = "Valor";
            ws.Cell(2, 1).Value = new DateTime(2026, 7, 1); ws.Cell(2, 3).Value = 100;
            ws.Cell(3, 1).Value = new DateTime(2026, 7, 2); ws.Cell(3, 2).Value = "Venda"; ws.Cell(3, 3).Value = 100;
        });

        var resultado = XlsxParser.Parse(stream);

        Assert.Single(resultado);
    }

    [Fact]
    public void Parse_ComCreditoEDebitoZerados_Ignora()
    {
        var stream = CriarPlanilha(ws =>
        {
            ws.Cell(1, 1).Value = "Data"; ws.Cell(1, 2).Value = "Historico";
            ws.Cell(1, 3).Value = "Credito"; ws.Cell(1, 4).Value = "Debito";
            ws.Cell(2, 1).Value = new DateTime(2026, 7, 1); ws.Cell(2, 2).Value = "Sem movimento";
            ws.Cell(2, 3).Value = 0; ws.Cell(2, 4).Value = 0;
        });

        var resultado = XlsxParser.Parse(stream);

        Assert.Empty(resultado);
    }

    [Fact]
    public void Parse_ComDataInvalida_Ignora()
    {
        var stream = CriarPlanilha(ws =>
        {
            ws.Cell(1, 1).Value = "Data"; ws.Cell(1, 2).Value = "Descricao"; ws.Cell(1, 3).Value = "Valor";
            ws.Cell(2, 1).Value = "não é data"; ws.Cell(2, 2).Value = "Venda"; ws.Cell(2, 3).Value = 100;
        });

        var resultado = XlsxParser.Parse(stream);

        Assert.Empty(resultado);
    }

    [Fact]
    public void Parse_ComValorNaoNumerico_Ignora()
    {
        var stream = CriarPlanilha(ws =>
        {
            ws.Cell(1, 1).Value = "Data"; ws.Cell(1, 2).Value = "Descricao"; ws.Cell(1, 3).Value = "Valor";
            ws.Cell(2, 1).Value = new DateTime(2026, 7, 1); ws.Cell(2, 2).Value = "Venda"; ws.Cell(2, 3).Value = "abc";
        });

        var resultado = XlsxParser.Parse(stream);

        Assert.Empty(resultado);
    }

    [Fact]
    public void Parse_PlanilhaSemLinhasDeDados_LancaExcecao()
    {
        var stream = CriarPlanilha(ws => ws.Cell(1, 1).Value = "Data");

        Assert.Throws<InvalidOperationException>(() => XlsxParser.Parse(stream));
    }

    [Fact]
    public void Parse_SemCabecalhoReconhecivel_LancaExcecao()
    {
        var stream = CriarPlanilha(ws =>
        {
            ws.Cell(1, 1).Value = "Coluna1"; ws.Cell(1, 2).Value = "Coluna2";
            ws.Cell(2, 1).Value = "abc"; ws.Cell(2, 2).Value = "def";
        });

        Assert.Throws<InvalidOperationException>(() => XlsxParser.Parse(stream));
    }

    [Fact]
    public void Parse_SemColunaDeValorNemCreditoDebito_LancaExcecao()
    {
        var stream = CriarPlanilha(ws =>
        {
            ws.Cell(1, 1).Value = "Data"; ws.Cell(1, 2).Value = "Descricao";
            ws.Cell(2, 1).Value = new DateTime(2026, 7, 1); ws.Cell(2, 2).Value = "Venda";
        });

        Assert.Throws<InvalidOperationException>(() => XlsxParser.Parse(stream));
    }
}
