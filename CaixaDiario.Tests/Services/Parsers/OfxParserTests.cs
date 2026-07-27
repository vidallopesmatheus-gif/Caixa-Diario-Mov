using System.Text;
using CaixaDiario.API.Services.Parsers;

namespace CaixaDiario.Tests.Services.Parsers;

public class OfxParserTests
{
    private static MemoryStream ParaStream(string conteudo) => new(Encoding.Latin1.GetBytes(conteudo));

    [Fact]
    public void Parse_SgmlComCreditEDebit_RetornaTransacoesComSinalCorreto()
    {
        var ofx = "<OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS><BANKTRANLIST>" +
            "<STMTTRN><TRNTYPE>CREDIT<TRNAMT>200.50<DTPOSTED>20260715<FITID>abc1<MEMO>Recebimento Cliente</STMTTRN>" +
            "<STMTTRN><TRNTYPE>DEBIT<TRNAMT>-75.30<DTPOSTED>20260716<FITID>abc2<MEMO>Pagamento  Fornecedor</STMTTRN>" +
            "</BANKTRANLIST></STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>";

        var resultado = OfxParser.Parse(ParaStream(ofx));

        Assert.Equal(2, resultado.Count);

        var entrada = resultado[0];
        Assert.Equal(new DateOnly(2026, 7, 15), entrada.Data);
        Assert.Equal(200.50m, entrada.Valor);
        Assert.Equal("Entrada", entrada.Tipo);
        Assert.Equal("Recebimento Cliente", entrada.Descricao);
        Assert.Equal("abc1", entrada.FitId);

        var saida = resultado[1];
        Assert.Equal(75.30m, saida.Valor);
        Assert.Equal("Saida", saida.Tipo);
        // MEMO com espaços duplicados deve ser normalizado para um único espaço.
        Assert.Equal("Pagamento Fornecedor", saida.Descricao);
    }

    [Fact]
    public void Parse_SgmlSemMemo_UsaNameComoDescricao()
    {
        var ofx = "<STMTTRN><TRNTYPE>CREDIT<TRNAMT>10.00<DTPOSTED>20260701<NAME>Deposito</STMTTRN>";

        var resultado = OfxParser.Parse(ParaStream(ofx));

        var t = Assert.Single(resultado);
        Assert.Equal("Deposito", t.Descricao);
        Assert.Null(t.FitId);
    }

    [Fact]
    public void Parse_SgmlComCamposObrigatoriosAusentes_IgnoraTransacao()
    {
        var ofx = "<STMTTRN><TRNTYPE>CREDIT<TRNAMT>10.00<MEMO>Sem data</STMTTRN>" +
                  "<STMTTRN><TRNTYPE>CREDIT<DTPOSTED>20260701<MEMO>Sem valor</STMTTRN>" +
                  "<STMTTRN><TRNTYPE>CREDIT<TRNAMT>abc<DTPOSTED>20260701<MEMO>Valor invalido</STMTTRN>" +
                  "<STMTTRN><TRNTYPE>CREDIT<TRNAMT>10.00<DTPOSTED>invalida<MEMO>Data invalida</STMTTRN>";

        var resultado = OfxParser.Parse(ParaStream(ofx));

        Assert.Empty(resultado);
    }

    [Fact]
    public void Parse_XmlOfx2_RetornaTransacoes()
    {
        var ofx = "<?xml version=\"1.0\"?>" +
            "<OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS><BANKTRANLIST>" +
            "<STMTTRN><TRNTYPE>CREDIT</TRNTYPE><TRNAMT>50.00</TRNAMT><DTPOSTED>20260710</DTPOSTED>" +
            "<FITID>xml1</FITID><MEMO>Venda balcao</MEMO></STMTTRN>" +
            "</BANKTRANLIST></STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>";

        var resultado = OfxParser.Parse(ParaStream(ofx));

        var t = Assert.Single(resultado);
        Assert.Equal(new DateOnly(2026, 7, 10), t.Data);
        Assert.Equal(50m, t.Valor);
        Assert.Equal("Entrada", t.Tipo);
        Assert.Equal("xml1", t.FitId);
    }

    [Fact]
    public void Parse_SemTransacoes_RetornaListaVazia()
    {
        var ofx = "<OFX><BANKMSGSRSV1></BANKMSGSRSV1></OFX>";

        var resultado = OfxParser.Parse(ParaStream(ofx));

        Assert.Empty(resultado);
    }
}
