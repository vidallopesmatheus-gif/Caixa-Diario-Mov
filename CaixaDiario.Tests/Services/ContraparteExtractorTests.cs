using CaixaDiario.API.Services;

namespace CaixaDiario.Tests.Services;

public class ContraparteExtractorTests
{
    [Fact]
    public void Extrair_PixComCpfMascaradoPeloBancoEBancoAgenciaConta_LimpaONomeENaoUsaComoDocumento()
    {
        var info = ContraparteExtractor.Extrair(
            "Transferência recebida pelo Pix - NOME COMPLETO - •••.123.456-•• - BCO C6 S.A. (0336) Agência: 1 Conta: 1234567-1");

        Assert.Equal("NOME COMPLETO", info.Nome);
        Assert.Equal("•••.123.456-••", info.DocumentoParcial);
        // Mascarado pelo banco: os dígitos reais são desconhecidos, não pode virar chave por documento.
        Assert.StartsWith("NOME:", info.Chave);
        Assert.Equal("NOME:NOME COMPLETO", info.Chave);
    }

    [Fact]
    public void Extrair_CodigoClienteAntesOuDepoisDoNome_ConvergemParaAMesmaChave()
    {
        var antes = ContraparteExtractor.Extrair("12.345.678 NOME DA PESSOA");
        var depois = ContraparteExtractor.Extrair("NOME DA PESSOA (12.345.678)");

        Assert.Equal("NOME DA PESSOA", antes.Nome);
        Assert.Equal("NOME DA PESSOA", depois.Nome);
        Assert.Equal(antes.Chave, depois.Chave);
        Assert.Null(antes.DocumentoParcial);
    }

    [Theory]
    [InlineData("Pix enviado - MERCADO CENTRAL LTDA")]
    [InlineData("Pix recebido - MERCADO CENTRAL LTDA")]
    [InlineData("Compra no débito - MERCADO CENTRAL LTDA")]
    [InlineData("Débito de cartão - MERCADO CENTRAL LTDA")]
    [InlineData("Transferência enviada pelo Pix - MERCADO CENTRAL LTDA")]
    public void Extrair_RemovePrefixosConhecidosDeBanco(string descricao)
    {
        var info = ContraparteExtractor.Extrair(descricao);

        Assert.Equal("MERCADO CENTRAL LTDA", info.Nome);
    }

    [Fact]
    public void Extrair_CnpjCompletoSemMascara_UsaComoChaveEMascaraParaExibicao()
    {
        var info = ContraparteExtractor.Extrair("Pagamento Fornecedor 12.345.678/0001-90");

        Assert.Equal("Pagamento Fornecedor", info.Nome);
        Assert.Equal("••.345.678/0001-••", info.DocumentoParcial);
        Assert.Equal("DOC:12345678000190", info.Chave);
    }

    [Fact]
    public void Extrair_CpfCompletoSemMascara_UsaComoChaveEMascaraParaExibicao()
    {
        var info = ContraparteExtractor.Extrair("Transferência enviada pelo Pix - FULANO DA SILVA - 111.222.333-44");

        Assert.Equal("FULANO DA SILVA", info.Nome);
        Assert.Equal("•••.222.333-••", info.DocumentoParcial);
        Assert.Equal("DOC:11122233344", info.Chave);
    }

    [Fact]
    public void Extrair_MesmoCnpjFormatosDiferentesDeDescricao_MesmaChave()
    {
        var a = ContraparteExtractor.Extrair("Pagamento Fornecedor 12.345.678/0001-90");
        var b = ContraparteExtractor.Extrair("Pix recebido - OUTRO NOME 12345678000190");

        Assert.Equal(a.Chave, b.Chave);
    }

    [Fact]
    public void Extrair_MesmaPessoaNomesComAcentuacaoDiferente_MesmaChavePorNome()
    {
        var a = ContraparteExtractor.Extrair("Pix enviado - João Pedro Ação");
        var b = ContraparteExtractor.Extrair("Pix enviado - JOAO PEDRO ACAO");

        Assert.Equal(a.Chave, b.Chave);
    }

    [Fact]
    public void Extrair_SemPrefixoConhecidoNemDocumento_MantemDescricaoComoNome()
    {
        var info = ContraparteExtractor.Extrair("Tarifa de manutenção de conta");

        Assert.Equal("Tarifa de manutenção de conta", info.Nome);
        Assert.Null(info.DocumentoParcial);
        Assert.Equal("NOME:TARIFA DE MANUTENCAO DE CONTA", info.Chave);
    }

    [Fact]
    public void Extrair_DescricaoVazia_NaoLancaExcecao()
    {
        var info = ContraparteExtractor.Extrair("");

        Assert.Equal("", info.Nome);
        Assert.Null(info.DocumentoParcial);
    }

    [Theory]
    [InlineData("12345678000190", "••.345.678/0001-••")]
    [InlineData("11122233344", "•••.222.333-••")]
    public void Mascarar_MantemSoOsBlocosDoMeio(string digitos, string esperado)
    {
        Assert.Equal(esperado, ContraparteExtractor.Mascarar(digitos));
    }
}
