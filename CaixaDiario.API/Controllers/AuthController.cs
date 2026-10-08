using CaixaDiario.API.DTOs.Auth;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    // Item 2.10: explícito mesmo sem política de autorização global hoje — login precisa
    // continuar público mesmo que uma fallback policy exigindo auth seja adicionada depois, e um
    // token antigo/inválido no header (de outra sessão) nunca pode impedir um login novo.
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        var resultado = await _authService.LoginAsync(dto);
        return Ok(new ApiResponse<LoginResponseDto> { Dados = resultado });
    }
}
