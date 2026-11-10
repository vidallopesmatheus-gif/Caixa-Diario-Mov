// CaixaDiario.API/Services/IAuthService.cs
using CaixaDiario.API.DTOs.Auth;

namespace CaixaDiario.API.Services;

// RefreshToken vai só pro cookie HttpOnly (nunca no corpo da resposta) — null quando o serviço roda
// sem repositório de refresh (testes antigos).
public record SessaoEmitida(LoginResponseDto Resposta, string? RefreshToken, DateTime? RefreshExpiraEm);

public interface IAuthService
{
    Task<SessaoEmitida> LoginAsync(LoginRequestDto dto);
    Task<SessaoEmitida> RenovarAsync(string? refreshToken);
    Task EncerrarAsync(string? refreshToken);
}
