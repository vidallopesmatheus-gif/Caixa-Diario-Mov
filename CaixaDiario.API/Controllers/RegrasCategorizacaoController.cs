using CaixaDiario.API.DTOs.Regras;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/regras-categorizacao")]
[Authorize]
public class RegrasCategorizacaoController : ControllerBase
{
    private readonly IRegraCategorizacaoService _service;

    public RegrasCategorizacaoController(IRegraCategorizacaoService service) => _service = service;

    private Guid ObterUsuarioId() => Guid.Parse(User.FindFirst("id")!.Value);
    private string ObterPerfil() => User.FindFirst("perfil")!.Value;

    [HttpGet("{clienteId:guid}")]
    public async Task<IActionResult> Listar(Guid clienteId)
    {
        var regras = await _service.ListarAsync(clienteId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<List<RegraCategorizacaoDto>> { Dados = regras });
    }

    [HttpPost("{clienteId:guid}")]
    public async Task<IActionResult> Criar(Guid clienteId, [FromBody] CriarRegraDto dto)
    {
        var criada = await _service.CriarAsync(clienteId, dto, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<RegraCategorizacaoDto> { Dados = criada });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] AtualizarRegraDto dto)
    {
        var atualizada = await _service.AtualizarAsync(id, dto, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<RegraCategorizacaoDto> { Dados = atualizada });
    }

    [HttpPost("{id:guid}/desativar")]
    public async Task<IActionResult> Desativar(Guid id)
    {
        await _service.DesativarAsync(id, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<object> { Dados = null });
    }

    [HttpPost("{id:guid}/reativar")]
    public async Task<IActionResult> Reativar(Guid id)
    {
        await _service.ReativarAsync(id, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<object> { Dados = null });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id)
    {
        await _service.ExcluirAsync(id, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<object> { Dados = null });
    }

    [HttpPut("{clienteId:guid}/reordenar")]
    public async Task<IActionResult> Reordenar(Guid clienteId, [FromBody] ReordenarRegrasDto dto)
    {
        await _service.ReordenarAsync(clienteId, dto.Ids, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<object> { Dados = null });
    }

    [HttpGet("contar-correspondencias")]
    public async Task<IActionResult> ContarCorrespondencias(
        [FromQuery] Guid contaBancariaId, [FromQuery] string tipo, [FromQuery] string descricaoReferencia)
    {
        var quantidade = await _service.ContarCorrespondenciasAsync(contaBancariaId, tipo, descricaoReferencia, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<ContagemCorrespondenciaDto> { Dados = new ContagemCorrespondenciaDto { Quantidade = quantidade } });
    }

    [HttpPost("{id:guid}/aplicar-retroativo")]
    public async Task<IActionResult> AplicarRetroativo(Guid id)
    {
        var resultado = await _service.AplicarRetroativamenteAsync(id, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<AplicarRetroativoResultDto> { Dados = resultado });
    }
}
