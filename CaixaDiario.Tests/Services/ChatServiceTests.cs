using CaixaDiario.API.DTOs.Chat;
using CaixaDiario.API.Services;
using Moq;

namespace CaixaDiario.Tests.Services;

public class ChatServiceTests
{
    private readonly Mock<IAnthropicClient> _clientMock = new();
    private readonly ChatService _sut;

    public ChatServiceTests()
    {
        _sut = new ChatService(_clientMock.Object);
    }

    [Fact]
    public async Task ResponderAsync_MensagemOffTopic_RetornaRespostaBloqueadaSemChamarAPI()
    {
        var dto = new ChatRequestDto { Message = "me dê uma receita de bolo", History = [] };

        var resultado = await _sut.ResponderAsync(dto);

        Assert.True(resultado.WasBlocked);
        Assert.Contains("Caixa Diário", resultado.Reply);
        _clientMock.Verify(c => c.EnviarMensagemAsync(It.IsAny<string>(), It.IsAny<List<ChatMessageDto>>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResponderAsync_MensagemValida_ChamaAPIERetornaResposta()
    {
        var dto = new ChatRequestDto
        {
            Message = "como adiciono uma entrada no caixa?",
            History = []
        };
        _clientMock
            .Setup(c => c.EnviarMensagemAsync(It.IsAny<string>(), It.IsAny<List<ChatMessageDto>>(), dto.Message))
            .ReturnsAsync("Na página Caixa, clique em adicionar entrada.");

        var resultado = await _sut.ResponderAsync(dto);

        Assert.False(resultado.WasBlocked);
        Assert.Equal("Na página Caixa, clique em adicionar entrada.", resultado.Reply);
        _clientMock.Verify(c => c.EnviarMensagemAsync(It.IsAny<string>(), It.IsAny<List<ChatMessageDto>>(), dto.Message), Times.Once);
    }

    [Fact]
    public async Task ResponderAsync_HistoricoComMaisDe10Mensagens_EnviaApenasUltimas10()
    {
        var historico = Enumerable.Range(1, 15)
            .Select(i => new ChatMessageDto { Role = "user", Content = $"msg {i}" })
            .ToList();
        var dto = new ChatRequestDto { Message = "como exportar?", History = historico };

        _clientMock
            .Setup(c => c.EnviarMensagemAsync(It.IsAny<string>(), It.IsAny<List<ChatMessageDto>>(), dto.Message))
            .ReturnsAsync("resposta");

        await _sut.ResponderAsync(dto);

        _clientMock.Verify(c => c.EnviarMensagemAsync(
            It.IsAny<string>(),
            It.Is<List<ChatMessageDto>>(h => h.Count == 10),
            dto.Message), Times.Once);
    }

    [Fact]
    public async Task ResponderAsync_MensagemVazia_ChamaAPINormalmente()
    {
        var dto = new ChatRequestDto { Message = "", History = [] };
        _clientMock
            .Setup(c => c.EnviarMensagemAsync(It.IsAny<string>(), It.IsAny<List<ChatMessageDto>>(), dto.Message))
            .ReturnsAsync("Como posso ajudar?");

        var resultado = await _sut.ResponderAsync(dto);

        Assert.False(resultado.WasBlocked);
    }
}
