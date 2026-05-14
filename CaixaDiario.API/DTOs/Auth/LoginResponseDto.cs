namespace CaixaDiario.API.DTOs.Auth;

public class LoginResponseDto
{
    public Guid Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string Perfil { get; set; } = string.Empty;
}
