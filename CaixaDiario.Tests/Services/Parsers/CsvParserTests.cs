using System.Text;
using CaixaDiario.API.Services.Parsers;

namespace CaixaDiario.Tests.Services.Parsers;

public class CsvParserTests
{
    private static MemoryStream ParaStream(string conteudo) => new(Encoding.Latin1.GetBytes(conteudo));

    [Fact]
    public void Parse_ComColunaUnicaDeValorPositivoENegativo_ClassificaEntradaESaida()
    {
        var csv = "Data;Descricao;Valor\n" +
                  "01/07/2026;Venda;100,00\n" +
                  "02/07/2026;Compra;-50,00\n";

        var resultado = CsvParser.Parse(ParaStream(csv));

        Assert.Equal(2, resultado.Count);
        Assert.Equal("Entrada", resultado[0].Tipo);
        Assert.Equal(100m, resultado[0].Valor);
        Assert.Equal("Saida", resultado[1].Tipo);
        Assert.Equal(50m, resultado[1].Valor);
    }

    [Fact]
    public void Parse_ComColunasSeparadasDeCreditoEDebito_ClassificaCorretamente()
    {
        var csv = "Data;Historico;Credito;Debito\n" +
                  "01/07/2026;Recebimento;300,00;\n" +
                  "02/07/2026;Pagamento;;150,00\n";

        var resultado = CsvParser.Parse(ParaStream(csv));

        Assert.Equal(2, resultado.Count);
        Assert.Equal("Entrada", resultado[0].Tipo);
        Assert.Equal(300m, resultado[0].Valor);
        Assert.Equal("Saida", resultado[1].Tipo);
        Assert.Equal(150m, resultado[1].Valor);
    }

    [Fact]
    public void Parse_ComDelimitadorVirgula_DetectaEProcessaCorretamente()
    {
        var csv = "Data,Descricao,Valor\n01/07/2026,Venda,100.00\n";

        var resultado = CsvParser.Parse(ParaStream(csv));

        var t = Assert.Single(resultado);
        Assert.Equal("Venda", t.Descricao);
    }

    [Fact]
    public void Parse_ComCamposEntreAspasContendoDelimitador_RespeitaAspas()
    {
        var csv = "Data;Descricao;Valor\n01/07/2026;\"Venda, item especial\";100,00\n";

        var resultado = CsvParser.Parse(ParaStream(csv));

        var t = Assert.Single(resultado);
        Assert.Equal("Venda, item especial", t.Descricao);
    }

    [Fact]
    public void Parse_ComLinhaComCredEDebZerados_IgnoraLinha()
    {
        var csv = "Data;Historico;Credito;Debito\n01/07/2026;Sem movimento;0;0\n";

        var resultado = CsvParser.Parse(ParaStream(csv));

        Assert.Empty(resultado);
    }

    [Fact]
    public void Parse_ComValorInvalido_IgnoraLinha()
    {
        var csv = "Data;Descricao;Valor\n01/07/2026;Venda;abc\n";

        var resultado = CsvParser.Parse(ParaStream(csv));

        Assert.Empty(resultado);
    }

    [Fact]
    public void Parse_ComDataInvalida_IgnoraLinha()
    {
        var csv = "Data;Descricao;Valor\ndata-invalida;Venda;100,00\n";

        var resultado = CsvParser.Parse(ParaStream(csv));

        Assert.Empty(resultado);
    }

    [Fact]
    public void Parse_ComLinhaEmBranco_Ignora()
    {
        var csv = "Data;Descricao;Valor\n\n01/07/2026;Venda;100,00\n";

        var resultado = CsvParser.Parse(ParaStream(csv));

        Assert.Single(resultado);
    }

    [Fact]
    public void Parse_ComLinhaComPoucasColunas_Ignora()
    {
        var csv = "Data;Descricao;Valor\n01/07/2026\n01/07/2026;Venda;100,00\n";

        var resultado = CsvParser.Parse(ParaStream(csv));

        Assert.Single(resultado);
    }

    [Fact]
    public void Parse_ArquivoVazio_LancaExcecao()
    {
        var csv = "Data;Descricao;Valor\n";

        Assert.Throws<InvalidOperationException>(() => CsvParser.Parse(ParaStream(csv)));
    }

    [Fact]
    public void Parse_SemCabecalhoReconhecivel_LancaExcecao()
    {
        var csv = "Coluna1;Coluna2\nabc;def\n";

        Assert.Throws<InvalidOperationException>(() => CsvParser.Parse(ParaStream(csv)));
    }

    [Fact]
    public void Parse_SemColunaDeValorNemCreditoDebito_LancaExcecao()
    {
        var csv = "Data;Descricao\n01/07/2026;Venda\n";

        Assert.Throws<InvalidOperationException>(() => CsvParser.Parse(ParaStream(csv)));
    }

    [Fact]
    public void Parse_SemColunaDeDescricaoNemFallback_LancaExcecao()
    {
        // Todas as colunas são identificadas como data/valor/crédito/débito — nenhuma sobra para descrição.
        var csv = "Data;Valor;Credito;Debito\n01/07/2026;100,00;0;0\n";

        Assert.Throws<InvalidOperationException>(() => CsvParser.Parse(ParaStream(csv)));
    }
}
