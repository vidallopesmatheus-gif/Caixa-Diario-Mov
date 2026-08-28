using CaixaDiario.API.DTOs.Metas;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/metas")]
[Authorize]
public class MetasController : ControllerBase
{
    private readonly IMetaService _metaService;

    public MetasController(IMetaService metaService) => _metaService = metaService;

    private Guid ObterUsuarioId() => Guid.Parse(User.FindFirst("id")!.Value);
    private string ObterPerfil() => User.FindFirst("perfil")!.Value;

    [HttpGet("{clienteId:guid}/{ano:int}")]
    public async Task<IActionResult> Obter(Guid clienteId, int ano)
    {
        var resultado = await _metaService.ObterMetaAsync(clienteId, ano, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<MetaAnualDto> { Dados = resultado });
    }

    [HttpGet("{clienteId:guid}")]
    public async Task<IActionResult> Listar(Guid clienteId)
    {
        var resultado = await _metaService.ListarMetasAsync(clienteId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<List<MetaAnualDto>> { Dados = resultado });
    }

    [HttpPost]
    public async Task<IActionResult> Salvar([FromBody] SalvarMetaAnualDto dto)
    {
        var resultado = await _metaService.SalvarMetaAsync(dto, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<MetaAnualDto> { Dados = resultado });
    }
}
