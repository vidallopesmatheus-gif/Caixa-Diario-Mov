using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using CaixaDiario.API.DTOs.Chat;

namespace CaixaDiario.API.Services;

public class AnthropicClientWrapper : IAnthropicClient
{
    private readonly AnthropicClient _client;
    private readonly string _model;
    private readonly int _maxTokens;

    public AnthropicClientWrapper(string apiKey, string model, int maxTokens)
    {
        _client = new AnthropicClient(apiKey);
        _model = model;
        _maxTokens = maxTokens;
    }

    public async Task<string> EnviarMensagemAsync(string systemPrompt, List<ChatMessageDto> historico, string mensagem)
    {
        var messages = historico
            .Select(m => new Message(
                m.Role == "user" ? RoleType.User : RoleType.Assistant,
                m.Content))
            .ToList();

        messages.Add(new Message(RoleType.User, mensagem));

        var parameters = new MessageParameters
        {
            Messages = messages,
            Model = _model,
            MaxTokens = _maxTokens,
            System = new List<SystemMessage> { new SystemMessage(systemPrompt) }
        };

        var response = await _client.Messages.GetClaudeMessageAsync(parameters);
        return response.Content?.OfType<TextContent>().FirstOrDefault()?.Text ?? string.Empty;
    }
}
