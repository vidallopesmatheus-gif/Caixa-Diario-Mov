// CaixaDiario.API/Services/IAuthService.cs
using CaixaDiario.API.DTOs.Auth;

namespace CaixaDiario.API.Services;

public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(LoginRequestDto dto);
}
