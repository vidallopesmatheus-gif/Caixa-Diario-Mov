using CaixaDiario.API.Services.Parsers;

namespace CaixaDiario.Tests.Services.Parsers;

public class ColunaMapperTests
{
    [Fact]
    public void EncontrarCabecalho_ComLinhaContendoData_RetornaIndice()
    {
        var linhas = new[] { "Extrato Bancario", "Data;Descricao;Valor", "01/01/2026;Venda;100" };

        var indice = ColunaMapper.EncontrarCabecalho(linhas);

        Assert.Equal(1, indice);
    }

    [Fact]
    public void EncontrarCabecalho_ComPalavraDateOuDt_RetornaIndice()
    {
        Assert.Equal(0, ColunaMapper.EncontrarCabecalho(new[] { "Date;Description;Amount" }));
        Assert.Equal(0, ColunaMapper.EncontrarCabecalho(new[] { "Dt;Historico;Valor" }));
    }

    [Fact]
    public void EncontrarCabecalho_SemLinhaCorrespondente_RetornaMenosUm()
    {
        var linhas = new[] { "Extrato Bancario", "Sem cabecalho reconhecivel" };

        var indice = ColunaMapper.EncontrarCabecalho(linhas);

        Assert.Equal(-1, indice);
    }

    [Fact]
    public void EncontrarCabecalho_RespeitaLimiteMaximoDeLinhas()
    {
        var linhas = new[] { "linha 0", "linha 1", "Data;Descricao" };

        var indice = ColunaMapper.EncontrarCabecalho(linhas, max: 2);

        Assert.Equal(-1, indice);
    }

    [Theory]
    [InlineData("Data;Historico;Valor", 0, 1, 2, -1, -1)]
    [InlineData("Data;Descricao;Amount", 0, 1, 2, -1, -1)]
    [InlineData("Date;Title;Vlr", 0, 1, 2, -1, -1)]
    [InlineData("Data;Memo;Value", 0, 1, 2, -1, -1)]
    [InlineData("Data;Lancamento;Valor", 0, 1, 2, -1, -1)]
    [InlineData("Data;Historico;Credito;Debito", 0, 1, -1, 2, 3)]
    [InlineData("Data;Historico;Entrada;Saida", 0, 1, -1, 2, 3)]
    [InlineData("Data;Historico;Recebido;Pagamento", 0, 1, -1, 2, 3)]
    public void MapearColunas_IdentificaColunasPorPalavraChave(
        string cabecalho, int data, int desc, int valor, int cred, int deb)
    {
        var cols = cabecalho.Split(';');

        var map = ColunaMapper.MapearColunas(cols);

        Assert.Equal(data, map.Data);
        Assert.Equal(desc, map.Descricao);
        Assert.Equal(valor, map.Valor);
        Assert.Equal(cred, map.Credito);
        Assert.Equal(deb, map.Debito);
    }

    [Fact]
    public void MapearColunas_SemColunaDeDescricaoReconhecivel_UsaPrimeiraColunaSobrando()
    {
        var cols = new[] { "Data", "XPTO", "Valor" };

        var map = ColunaMapper.MapearColunas(cols);

        Assert.Equal(0, map.Data);
        Assert.Equal(2, map.Valor);
        Assert.Equal(1, map.Descricao); // única coluna que sobra
    }

    [Theory]
    [InlineData("01/01/2026")]
    [InlineData("2026-01-01")]
    [InlineData("01-01-2026")]
    public void ParseData_ComFormatosValidos_RetornaData(string entrada)
    {
        var data = ColunaMapper.ParseData(entrada);

        Assert.Equal(new DateOnly(2026, 1, 1), data);
    }

    [Fact]
    public void ParseData_FormatoAmericano_RetornaData()
    {
        var data = ColunaMapper.ParseData("12/25/2026");

        Assert.Equal(new DateOnly(2026, 12, 25), data);
    }

    [Fact]
    public void ParseData_FormatoCompacto_RetornaData()
    {
        var data = ColunaMapper.ParseData("20260101");

        Assert.Equal(new DateOnly(2026, 1, 1), data);
    }

    [Fact]
    public void ParseData_FormatoInvalido_RetornaNull()
    {
        Assert.Null(ColunaMapper.ParseData("não é uma data"));
    }

    [Theory]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("100,00", 100.00)]
    [InlineData("100.50", 100.50)]
    [InlineData("R$ 50,00", 50.00)]
    public void ParseDecimalBr_ComFormatosValidos_ConverteCorretamente(string entrada, double esperado)
    {
        var sucesso = ColunaMapper.ParseDecimalBr(entrada, out var resultado);

        Assert.True(sucesso);
        Assert.Equal((decimal)esperado, resultado);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-")]
    [InlineData("abc")]
    public void ParseDecimalBr_ComEntradaInvalida_RetornaFalse(string entrada)
    {
        var sucesso = ColunaMapper.ParseDecimalBr(entrada, out var resultado);

        Assert.False(sucesso);
        Assert.Equal(0m, resultado);
    }

    [Fact]
    public void CampoSafe_ComIndiceValido_RetornaValor()
    {
        var campos = new[] { "a", "b", "c" };

        Assert.Equal("b", ColunaMapper.CampoSafe(campos, 1));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void CampoSafe_ComIndiceForaDoIntervalo_RetornaVazio(int idx)
    {
        var campos = new[] { "a", "b", "c" };

        Assert.Equal(string.Empty, ColunaMapper.CampoSafe(campos, idx));
    }
}
