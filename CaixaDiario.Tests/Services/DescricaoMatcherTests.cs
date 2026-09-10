using CaixaDiario.API.Services;

namespace CaixaDiario.Tests.Services;

public class DescricaoMatcherTests
{
    [Fact]
    public void ExtrairDocumento_DescricaoComCnpjFormatado_RetornaDocNormalizado()
    {
        var resultado = DescricaoMatcher.ExtrairDocumento(
            "Transferência enviada pelo Pix - AUTOPASS S.A. - 07.140.538/0001-40 - BCO BRADESCO S.A. (0237) Agência: 3394 Conta: 4050-9");
        Assert.Equal("DOC:07140538000140", resultado);
    }

    [Fact]
    public void ExtrairDocumento_DescricaoComCpf_RetornaDocNormalizado()
    {
        var resultado = DescricaoMatcher.ExtrairDocumento("Pix recebido de Fulano de Tal - 123.456.789-00");
        Assert.Equal("DOC:12345678900", resultado);
    }

    [Fact]
    public void ExtrairDocumento_SemDocumento_RetornaNull()
    {
        Assert.Null(DescricaoMatcher.ExtrairDocumento("Aplicação RDB"));
    }

    [Fact]
    public void DeterminarCriterio_ComCnpj_UsaDocumento()
    {
        var (tipo, valor) = DescricaoMatcher.DeterminarCriterio("Pagamento - 07.140.538/0001-40");
        Assert.Equal("Documento", tipo);
        Assert.Equal("DOC:07140538000140", valor);
    }

    [Fact]
    public void DeterminarCriterio_SemDocumento_UsaDescricaoExataNormalizada()
    {
        var (tipo, valor) = DescricaoMatcher.DeterminarCriterio("  aplicação rdb  ");
        Assert.Equal("DescricaoExata", tipo);
        Assert.Equal("APLICAÇÃO RDB", valor);
    }

    [Theory]
    [InlineData("Aplicação RDB", "APLICAÇÃO RDB", true)]
    [InlineData("aplicação rdb", "APLICAÇÃO RDB", true)]
    [InlineData("Resgate RDB", "APLICAÇÃO RDB", false)]
    public void Casa_CriterioDescricaoExata_ComparaNormalizado(string descricao, string criterioValor, bool esperado)
    {
        Assert.Equal(esperado, DescricaoMatcher.Casa("DescricaoExata", criterioValor, descricao));
    }

    [Fact]
    public void Casa_CriterioDocumento_IgnoraTextoAoRedorDoCnpj()
    {
        var casaComOutroFavorecido = DescricaoMatcher.Casa(
            "Documento", "DOC:07140538000140",
            "Transferência enviada pelo Pix - OUTRO NOME LTDA - 07.140.538/0001-40 - BCO ITAU (0341) Agência: 1 Conta: 2");
        Assert.True(casaComOutroFavorecido);
    }

    [Fact]
    public void Casa_CriterioDocumento_DocumentoDiferenteNaoCasa()
    {
        var casa = DescricaoMatcher.Casa("Documento", "DOC:07140538000140", "Pix para 11.222.333/0001-99");
        Assert.False(casa);
    }
}
