namespace CaixaDiario.API.DTOs.Auth;

public class LoginResponseDto
{
    public Guid UsuarioId { get; set; }
    public string Token { get; set; } = string.Empty;
    // Quando o access token vence (UTC) — o front renova antes, sem esperar o 401.
    public DateTime ExpiraEm { get; set; }
    public string NomeCompleto { get; set; } = string.Empty;
    public string NomeUsuario { get; set; } = string.Empty;
    public string NomeEstabelecimento { get; set; } = string.Empty;
    public string Perfil { get; set; } = string.Empty;
}
