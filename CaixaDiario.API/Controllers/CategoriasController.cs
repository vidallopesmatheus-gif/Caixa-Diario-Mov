using CaixaDiario.API.DTOs.Categorias;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

// Correção de integridade (Parte 1): categoria deixou de ser global — toda rota agora vive sob
// o cliente dono dela (clienteId na URL), igual ContasBancariasController/MetasController etc.
[ApiController]
[Route("api/clientes/{clienteId:guid}/categorias")]
[Authorize]
public class CategoriasController : ControllerBase
{
    private readonly ICategoriaService _service;

    public CategoriasController(ICategoriaService service) => _service = service;

    private Guid ObterUsuarioId() => Guid.Parse(User.FindFirst("id")!.Value);
    private string ObterPerfil() => User.FindFirst("perfil")!.Value;

    // Formato legado consumido pelos formulários de lançamento — não envolve ApiResponse.
    [HttpGet]
    public async Task<IActionResult> Listar(Guid clienteId)
    {
        var categorias = await _service.ListarAgrupadasAsync(clienteId);
        return Ok(categorias);
    }

    [HttpGet("gerenciar")]
    public async Task<IActionResult> ListarParaGerenciar(Guid clienteId)
    {
        var categorias = await _service.ListarParaGerenciarAsync(clienteId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<List<CategoriaDto>> { Dados = categorias });
    }

    [HttpPost]
    public async Task<IActionResult> Criar(Guid clienteId, [FromBody] CriarCategoriaDto dto)
    {
        var criada = await _service.CriarAsync(clienteId, dto, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<CategoriaDto> { Dados = criada });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid clienteId, Guid id, [FromBody] AtualizarCategoriaDto dto)
    {
        var atualizada = await _service.AtualizarAsync(clienteId, id, dto, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<CategoriaDto> { Dados = atualizada });
    }

    [HttpPost("{id:guid}/desativar")]
    public async Task<IActionResult> Desativar(Guid clienteId, Guid id)
    {
        await _service.DesativarAsync(clienteId, id, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<object> { Dados = null });
    }

    [HttpPut("reordenar")]
    public async Task<IActionResult> Reordenar(Guid clienteId, [FromBody] ReordenarCategoriasDto dto)
    {
        await _service.ReordenarAsync(clienteId, dto, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<object> { Dados = null });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid clienteId, Guid id)
    {
        var resultado = await _service.ExcluirOuInformarUsoAsync(clienteId, id, ObterUsuarioId(), ObterPerfil());
        if (!resultado.Excluida)
            return Conflict(new ApiResponse<ExclusaoCategoriaResultDto> { Dados = resultado });
        return Ok(new ApiResponse<ExclusaoCategoriaResultDto> { Dados = resultado });
    }

    [HttpPost("{id:guid}/migrar")]
    public async Task<IActionResult> Migrar(Guid clienteId, Guid id, [FromBody] MigrarCategoriaDto dto)
    {
        await _service.MigrarLancamentosAsync(clienteId, id, dto.ParaCategoriaId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<object> { Dados = null });
    }
}
