using CaixaDiario.API.DTOs.Categorias;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/categorias")]
[Authorize]
public class CategoriasController : ControllerBase
{
    private readonly ICategoriaService _service;

    public CategoriasController(ICategoriaService service) => _service = service;

    // Formato legado consumido pelos formulários de lançamento — não envolve ApiResponse.
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var categorias = await _service.ListarAgrupadasAsync();
        return Ok(categorias);
    }

    [HttpGet("gerenciar")]
    public async Task<IActionResult> ListarParaGerenciar()
    {
        var categorias = await _service.ListarParaGerenciarAsync();
        return Ok(new ApiResponse<List<CategoriaDto>> { Dados = categorias });
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarCategoriaDto dto)
    {
        var criada = await _service.CriarAsync(dto);
        return Ok(new ApiResponse<CategoriaDto> { Dados = criada });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] AtualizarCategoriaDto dto)
    {
        var atualizada = await _service.AtualizarAsync(id, dto);
        return Ok(new ApiResponse<CategoriaDto> { Dados = atualizada });
    }

    [HttpPost("{id:guid}/desativar")]
    public async Task<IActionResult> Desativar(Guid id)
    {
        await _service.DesativarAsync(id);
        return Ok(new ApiResponse<object> { Dados = null });
    }

    [HttpPut("reordenar")]
    public async Task<IActionResult> Reordenar([FromBody] ReordenarCategoriasDto dto)
    {
        await _service.ReordenarAsync(dto);
        return Ok(new ApiResponse<object> { Dados = null });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id)
    {
        var resultado = await _service.ExcluirOuInformarUsoAsync(id);
        if (!resultado.Excluida)
            return Conflict(new ApiResponse<ExclusaoCategoriaResultDto> { Dados = resultado });
        return Ok(new ApiResponse<ExclusaoCategoriaResultDto> { Dados = resultado });
    }

    [HttpPost("{id:guid}/migrar")]
    public async Task<IActionResult> Migrar(Guid id, [FromBody] MigrarCategoriaDto dto)
    {
        await _service.MigrarLancamentosAsync(id, dto.ParaCategoriaId);
        return Ok(new ApiResponse<object> { Dados = null });
    }
}
