using CaixaDiario.API.DTOs.Usuarios;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

// Bloco 3 da rodada "Visão de Futuro": preferências da tela "Meu Futuro" (reserva mínima,
// PF/PJ) — leitura/escrita pelo próprio cliente, sem passar pelo CRUD de Usuario (admin-only).
[ApiController]
[Route("api/preferencias-futuro")]
[Authorize]
public class PreferenciasFuturoController : ControllerBase
{
    private readonly IPreferenciasFuturoService _service;

    public PreferenciasFuturoController(IPreferenciasFuturoService service) => _service = service;

    private Guid ObterUsuarioId() => Guid.Parse(User.FindFirst("id")!.Value);
    private string ObterPerfil() => User.FindFirst("perfil")!.Value;

    [HttpGet("{clienteId:guid}")]
    public async Task<IActionResult> Obter(Guid clienteId)
    {
        var resultado = await _service.ObterAsync(clienteId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<PreferenciasFuturoDto> { Dados = resultado });
    }

    [HttpPut("{clienteId:guid}")]
    public async Task<IActionResult> Atualizar(Guid clienteId, [FromBody] AtualizarPreferenciasFuturoDto dto)
    {
        var resultado = await _service.AtualizarAsync(clienteId, ObterUsuarioId(), ObterPerfil(), dto);
        return Ok(new ApiResponse<PreferenciasFuturoDto> { Dados = resultado });
    }
}
