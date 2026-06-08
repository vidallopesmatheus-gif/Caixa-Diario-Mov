using CaixaDiario.API.Services;

namespace CaixaDiario.Tests.Services;

public class TopicGuardTests
{
    [Theory]
    [InlineData("me dê uma receita de bolo")]
    [InlineData("qual é a receita de frango assado?")]
    [InlineData("previsão do tempo para amanhã")]
    [InlineData("qual o clima em São Paulo hoje?")]
    [InlineData("temperatura em Recife")]
    [InlineData("quem ganhou o futebol ontem?")]
    [InlineData("me fale sobre política")]
    [InlineData("quais são as últimas notícias?")]
    [InlineData("como instalar o Python?")]
    [InlineData("tutorial de programação em C#")]
    public void IsOffTopic_MensagensForaDoEscopo_RetornaTrue(string mensagem)
    {
        var resultado = TopicGuard.IsOffTopic(mensagem);
        Assert.True(resultado);
    }

    [Theory]
    [InlineData("como adiciono uma entrada no caixa?")]
    [InlineData("o que é uma conta provisionada?")]
    [InlineData("como exportar meus dados?")]
    [InlineData("como funciona a meta anual?")]
    [InlineData("qual é o saldo do dashboard?")]
    [InlineData("como ver o histórico de registros?")]
    [InlineData("não entendo o gráfico de gastos")]
    [InlineData("como faço login?")]
    [InlineData("o que são saídas no caixa diário?")]
    public void IsOffTopic_MensagensDentroDoEscopo_RetornaFalse(string mensagem)
    {
        var resultado = TopicGuard.IsOffTopic(mensagem);
        Assert.False(resultado);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void IsOffTopic_MensagemVazia_RetornaFalse(string mensagem)
    {
        var resultado = TopicGuard.IsOffTopic(mensagem);
        Assert.False(resultado);
    }

    [Theory]
    [InlineData("qual a receita de vendas em janeiro?")]
    [InlineData("como vejo minha receita mensal?")]
    public void IsOffTopic_ReceitaFinanceira_RetornaFalse(string mensagem)
    {
        var resultado = TopicGuard.IsOffTopic(mensagem);
        Assert.False(resultado);
    }
}
