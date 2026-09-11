using CaixaDiario.API.DTOs.LinksConciliacao;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/links-conciliacao")]
[Authorize]
public class LinksConciliacaoController : ControllerBase
{
    private readonly ILinkConciliacaoService _service;

    public LinksConciliacaoController(ILinkConciliacaoService service) => _service = service;

    private Guid ObterUsuarioId() => Guid.Parse(User.FindFirst("id")!.Value);
    private string ObterPerfil() => User.FindFirst("perfil")!.Value;

    [HttpPost("{clienteId:guid}/gerar")]
    public async Task<IActionResult> Gerar(Guid clienteId)
    {
        var link = await _service.GerarAsync(clienteId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<LinkConciliacaoDto> { Dados = link });
    }

    [HttpGet("{clienteId:guid}")]
    public async Task<IActionResult> Listar(Guid clienteId)
    {
        var links = await _service.ListarAsync(clienteId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<List<LinkConciliacaoDto>> { Dados = links });
    }

    [HttpPost("{id:guid}/revogar")]
    public async Task<IActionResult> Revogar(Guid id)
    {
        await _service.RevogarAsync(id, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<object> { Dados = null });
    }
}
