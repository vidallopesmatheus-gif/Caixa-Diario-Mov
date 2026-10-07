using CaixaDiario.API.DTOs.Conciliacao;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

// Fase 1.2/1.3: sugestões de vínculo entre títulos pendentes e lançamentos já importados.
[ApiController]
[Route("api/conciliacao")]
[Authorize]
public class ConciliacaoController : ControllerBase
{
    private readonly IConciliacaoService _service;

    public ConciliacaoController(IConciliacaoService service) => _service = service;

    private Guid ObterUsuarioId() => Guid.Parse(User.FindFirst("id")!.Value);
    private string ObterPerfil() => User.FindFirst("perfil")!.Value;

    [HttpGet("{clienteId:guid}/sugestoes")]
    public async Task<IActionResult> ListarSugestoes(Guid clienteId, [FromQuery] DateOnly de, [FromQuery] DateOnly ate, [FromQuery] Guid? contaBancariaId = null)
    {
        var resultado = await _service.ListarSugestoesAsync(clienteId, de, ate, ObterUsuarioId(), ObterPerfil(), contaBancariaId);
        return Ok(new ApiResponse<List<SugestaoVinculoDto>> { Dados = resultado });
    }
}
