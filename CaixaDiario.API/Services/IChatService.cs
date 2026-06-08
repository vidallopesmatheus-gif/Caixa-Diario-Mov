using CaixaDiario.API.DTOs.Chat;

namespace CaixaDiario.API.Services;

public interface IChatService
{
    Task<ChatResponseDto> ResponderAsync(ChatRequestDto dto);
}
