namespace CaixaDiario.API.DTOs.Chat;

public class ChatResponseDto
{
    public string Reply { get; set; } = string.Empty;
    public bool WasBlocked { get; set; }
}
