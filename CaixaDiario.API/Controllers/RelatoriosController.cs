using CaixaDiario.API.DTOs.Relatorios;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

// Fase 1.5: relatório Previsto × Realizado.
[ApiController]
[Route("api/relatorios")]
[Authorize]
public class RelatoriosController : ControllerBase
{
    private readonly IPrevistoRealizadoService _service;

    public RelatoriosController(IPrevistoRealizadoService service) => _service = service;

    private Guid ObterUsuarioId() => Guid.Parse(User.FindFirst("id")!.Value);
    private string ObterPerfil() => User.FindFirst("perfil")!.Value;

    [HttpGet("{clienteId:guid}/previsto-realizado")]
    public async Task<IActionResult> ObterPrevistoRealizado(
        Guid clienteId, [FromQuery] int meses = 6, [FromQuery] bool incluirAvulsosVinculados = false)
    {
        var resultado = await _service.ObterAsync(clienteId, meses, ObterUsuarioId(), ObterPerfil(), incluirAvulsosVinculados);
        return Ok(new ApiResponse<PrevistoRealizadoDto> { Dados = resultado });
    }
}
