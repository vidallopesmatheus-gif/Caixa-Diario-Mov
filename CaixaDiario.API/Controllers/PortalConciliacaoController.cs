using CaixaDiario.API.DTOs.PortalConciliacao;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

// Única controller pública do backend (fora do login) — o cliente acessa pelo token do link,
// nunca com um JWT. Ver ConciliacaoPublicaService pra validação do token e escopo dos dados.
[ApiController]
[Route("api/portal-conciliacao")]
[AllowAnonymous]
public class PortalConciliacaoController : ControllerBase
{
    private readonly IConciliacaoPublicaService _service;

    public PortalConciliacaoController(IConciliacaoPublicaService service) => _service = service;

    [HttpGet("{token}")]
    public async Task<IActionResult> ObterPendentes(string token)
    {
        var dados = await _service.ObterPendentesAsync(token);
        return Ok(new ApiResponse<PortalConciliacaoDto> { Dados = dados });
    }

    [HttpPost("{token}/classificar")]
    public async Task<IActionResult> Classificar(string token, [FromBody] ClassificarPendentesDto dto)
    {
        await _service.ClassificarAsync(token, dto);
        return Ok(new ApiResponse<object> { Dados = null });
    }
}
