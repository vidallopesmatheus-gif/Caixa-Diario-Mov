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
    // Money Vision ("desloga toda hora"): o refresh token só existe neste cookie — HttpOnly (JS
    // nenhum lê, nem num XSS), SameSite=Strict e restrito a /api/auth (não vai em toda requisição).
    public const string CookieRefresh = "mv_refresh";
    private const string CaminhoCookie = "/api/auth";

    private readonly IAuthService _authService;
    private readonly IHostEnvironment? _env;

    public AuthController(IAuthService authService, IHostEnvironment? env = null)
    {
        _authService = authService;
        _env = env;
    }

    // Item 2.10: explícito mesmo sem política de autorização global hoje — login precisa
    // continuar público mesmo que uma fallback policy exigindo auth seja adicionada depois, e um
    // token antigo/inválido no header (de outra sessão) nunca pode impedir um login novo.
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        var sessao = await _authService.LoginAsync(dto);
        GravarCookie(sessao);
        return Ok(new ApiResponse<LoginResponseDto> { Dados = sessao.Resposta });
    }

    // Chamado pelo apiFetch quando o access token vence: troca o cookie por um novo e devolve um
    // access token novo. Anônimo de propósito — quem autentica aqui é o cookie, não o JWT vencido.
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh()
    {
        try
        {
            var sessao = await _authService.RenovarAsync(Request.Cookies[CookieRefresh]);
            GravarCookie(sessao);
            return Ok(new ApiResponse<LoginResponseDto> { Dados = sessao.Resposta });
        }
        catch
        {
            ApagarCookie();
            throw;
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout()
    {
        await _authService.EncerrarAsync(Request.Cookies[CookieRefresh]);
        ApagarCookie();
        return Ok(new ApiResponse<object> { Dados = null });
    }

    private CookieOptions OpcoesCookie(DateTimeOffset? expira) => new()
    {
        HttpOnly = true,
        // Em desenvolvimento a API roda em http://localhost — um cookie Secure não seria aceito.
        Secure = _env?.IsDevelopment() != true,
        SameSite = SameSiteMode.Strict,
        Path = CaminhoCookie,
        Expires = expira,
    };

    private void GravarCookie(SessaoEmitida sessao)
    {
        if (sessao.RefreshToken == null) return;
        Response.Cookies.Append(CookieRefresh, sessao.RefreshToken, OpcoesCookie(sessao.RefreshExpiraEm));
    }

    private void ApagarCookie() => Response.Cookies.Delete(CookieRefresh, OpcoesCookie(null));
}
