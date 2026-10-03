using CaixaDiario.API.DTOs.Categorias;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
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

    // Grupo é global (sem ClienteId) — leitura fica aberta pra qualquer usuário autenticado, mas
    // mutação é restrita a admin, senão um cliente altera a taxonomia compartilhada de todos.
    private void VerificarAdmin()
    {
        if (User.FindFirst("perfil")?.Value != "admin")
            throw new ApiException(403, CodigoRetorno.SEM_PERMISSAO, "Acesso restrito a administradores.");
    }

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
        VerificarAdmin();
        var criado = await _service.CriarAsync(dto);
        return Ok(new ApiResponse<GrupoDto> { Dados = criado });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] AtualizarGrupoDto dto)
    {
        VerificarAdmin();
        var atualizado = await _service.AtualizarAsync(id, dto);
        return Ok(new ApiResponse<GrupoDto> { Dados = atualizado });
    }

    [HttpPost("{id:guid}/desativar")]
    public async Task<IActionResult> Desativar(Guid id)
    {
        VerificarAdmin();
        await _service.DesativarAsync(id);
        return Ok(new ApiResponse<object> { Dados = null });
    }

    [HttpPut("reordenar")]
    public async Task<IActionResult> Reordenar([FromBody] ReordenarGruposDto dto)
    {
        VerificarAdmin();
        await _service.ReordenarAsync(dto);
        return Ok(new ApiResponse<object> { Dados = null });
    }
}
