namespace CaixaDiario.API.DTOs.Chat;

public class ChatMessageDto
{
    public string Role { get; set; } = string.Empty;    // "user" ou "assistant"
    public string Content { get; set; } = string.Empty;
}
