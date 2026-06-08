using CaixaDiario.API.DTOs.Chat;

namespace CaixaDiario.API.Services;

public class ChatService : IChatService
{
    private const string SystemPrompt =
        """
        Você é o assistente do Caixa Diário, um aplicativo de controle financeiro pessoal.
        Você responde APENAS perguntas relacionadas a:
        - Como usar o app (registros diários, entradas, saídas)
        - Contas provisionadas
        - Metas anuais
        - Dashboard e gráficos
        - Exportação de dados
        - Dúvidas sobre finanças pessoais básicas no contexto do app

        Se o usuário perguntar qualquer coisa fora desse escopo, responda:
        "Só posso ajudar com o Caixa Diário. Tem alguma dúvida sobre registros, metas ou contas?"

        Seja direto e conciso. Responda sempre em português.
        """;

    private const string RespostaBloqueada =
        "Só consigo ajudar com perguntas sobre o Caixa Diário. " +
        "Tente perguntar sobre registros, metas, contas ou como usar o app.";

    private const int MaxHistorico = 10;

    private readonly IAnthropicClient _anthropicClient;

    public ChatService(IAnthropicClient anthropicClient)
    {
        _anthropicClient = anthropicClient;
    }

    public async Task<ChatResponseDto> ResponderAsync(ChatRequestDto dto)
    {
        if (TopicGuard.IsOffTopic(dto.Message))
            return new ChatResponseDto { Reply = RespostaBloqueada, WasBlocked = true };

        var historico = dto.History.TakeLast(MaxHistorico).ToList();
        var reply = await _anthropicClient.EnviarMensagemAsync(SystemPrompt, historico, dto.Message);

        return new ChatResponseDto { Reply = reply, WasBlocked = false };
    }
}
