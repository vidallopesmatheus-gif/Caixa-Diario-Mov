using CaixaDiario.API.DTOs.Chat;

namespace CaixaDiario.API.Services;

public interface IAnthropicClient
{
    Task<string> EnviarMensagemAsync(string systemPrompt, List<ChatMessageDto> historico, string mensagem);
}
