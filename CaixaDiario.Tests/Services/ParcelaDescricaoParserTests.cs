using CaixaDiario.API.Services;

namespace CaixaDiario.Tests.Services;

public class ParcelaDescricaoParserTests
{
    [Theory]
    [InlineData("Trocafone - Parcela 1/10", "Trocafone", 1, 10)]
    [InlineData("Airbnb * Hmmanqmbm2 - Parcela 5/6", "Airbnb * Hmmanqmbm2", 5, 6)]
    [InlineData("Pg *Libano Educacional - Parcela 1/3", "Pg *Libano Educacional", 1, 3)]
    [InlineData("Hbl*Hblthomytalks - Parcela 11/12", "Hbl*Hblthomytalks", 11, 12)]
    // Variações de espaçamento/caixa — mesmo padrão real, tolerante a ruído comum de extrato.
    [InlineData("Amazon-Parcela 3/6", "Amazon", 3, 6)]
    [InlineData("Loja X - parcela 2/4", "Loja X", 2, 4)]
    public void TentarExtrair_PadraoRealDeExtratoDeCartao_ExtraiComercianteNumeroETotal(
        string descricao, string comercianteEsperado, int numeroEsperado, int totalEsperado)
    {
        var resultado = ParcelaDescricaoParser.TentarExtrair(descricao);

        Assert.NotNull(resultado);
        Assert.Equal(comercianteEsperado, resultado!.Comerciante);
        Assert.Equal(numeroEsperado, resultado.Numero);
        Assert.Equal(totalEsperado, resultado.Total);
    }

    [Theory]
    [InlineData("Ifd*48611375 Rafael Vi")]
    [InlineData("Apple.Com/Bill")]
    [InlineData("Smiles Clube Smiles")]
    [InlineData("")]
    [InlineData(null)]
    public void TentarExtrair_SemPadraoDeParcela_RetornaNulo(string? descricao)
    {
        Assert.Null(ParcelaDescricaoParser.TentarExtrair(descricao));
    }

    // Rodada de ajustes de parcelamentos: outros formatos de banco além de "Parcela N/M".
    [Theory]
    [InlineData("Magazine Luiza PARC 02/08", "Magazine Luiza", 2, 8)]
    [InlineData("Magazine Luiza PARC.02/08", "Magazine Luiza", 2, 8)]
    [InlineData("Casas Bahia (3 de 12)", "Casas Bahia", 3, 12)]
    [InlineData("Trocafone 1/10", "Trocafone", 1, 10)]
    public void TentarExtrair_OutrosFormatosDeBanco_ExtraiComercianteNumeroETotal(
        string descricao, string comercianteEsperado, int numeroEsperado, int totalEsperado)
    {
        var resultado = ParcelaDescricaoParser.TentarExtrair(descricao);

        Assert.NotNull(resultado);
        Assert.Equal(comercianteEsperado, resultado!.Comerciante);
        Assert.Equal(numeroEsperado, resultado.Numero);
        Assert.Equal(totalEsperado, resultado.Total);
    }

    [Fact]
    public void TentarExtrair_UmaDeUma_RetornaNulo()
    {
        // "1/1" não é parcelamento de verdade — mesmo com o padrão explícito "Parcela".
        Assert.Null(ParcelaDescricaoParser.TentarExtrair("Loja - Parcela 1/1"));
    }

    [Theory]
    [InlineData("Loja - Parcela 0/5")] // numero < 1
    [InlineData("Loja - Parcela 7/5")] // numero > total
    public void TentarExtrair_NumeroInconsistente_RetornaNulo(string descricao)
    {
        Assert.Null(ParcelaDescricaoParser.TentarExtrair(descricao));
    }
}
