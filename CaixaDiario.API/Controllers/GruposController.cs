using CaixaDiario.API.DTOs.Categorias;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/grupos")]
[Authorize]
public class GruposController : ControllerBase
{
    private readonly IGrupoService _service;

    public GruposController(IGrupoService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var grupos = await _service.ListarTodosAsync();
        return Ok(new ApiResponse<List<GrupoDto>> { Dados = grupos });
    }

    [HttpGet("blocos")]
    public async Task<IActionResult> ListarBlocos()
    {
        var blocos = await _service.ListarBlocosAsync();
        return Ok(new ApiResponse<string[]> { Dados = blocos });
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarGrupoDto dto)
    {
        var criado = await _service.CriarAsync(dto);
        return Ok(new ApiResponse<GrupoDto> { Dados = criado });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] AtualizarGrupoDto dto)
    {
        var atualizado = await _service.AtualizarAsync(id, dto);
        return Ok(new ApiResponse<GrupoDto> { Dados = atualizado });
    }

    [HttpPost("{id:guid}/desativar")]
    public async Task<IActionResult> Desativar(Guid id)
    {
        await _service.DesativarAsync(id);
        return Ok(new ApiResponse<object> { Dados = null });
    }

    [HttpPut("reordenar")]
    public async Task<IActionResult> Reordenar([FromBody] ReordenarGruposDto dto)
    {
        await _service.ReordenarAsync(dto);
        return Ok(new ApiResponse<object> { Dados = null });
    }
}
