using CaixaDiario.API.DTOs.ContasRecorrentes;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/contas-recorrentes")]
[Authorize]
public class ContasRecorrentesController : ControllerBase
{
    private readonly IContaRecorrenteService _service;

    public ContasRecorrentesController(IContaRecorrenteService service) => _service = service;

    private Guid ObterUsuarioId() => Guid.Parse(User.FindFirst("id")!.Value);
    private string ObterPerfil() => User.FindFirst("perfil")!.Value;

    [HttpGet("{clienteId:guid}")]
    public async Task<IActionResult> Listar(Guid clienteId)
    {
        var result = await _service.ListarPorClienteAsync(clienteId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<List<ContaRecorrenteDto>> { Dados = result });
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarContaRecorrenteDto dto)
    {
        var result = await _service.CriarAsync(dto, ObterUsuarioId(), ObterPerfil());
        return CreatedAtAction(nameof(Listar), new { clienteId = result.ClienteId },
            new ApiResponse<ContaRecorrenteDto> { Dados = result });
    }

    [HttpPut("{clienteId:guid}/{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid clienteId, Guid id, [FromBody] AtualizarContaRecorrenteDto dto)
    {
        var result = await _service.AtualizarAsync(clienteId, id, dto, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<ContaRecorrenteDto> { Dados = result });
    }

    [HttpDelete("{clienteId:guid}/{id:guid}")]
    public async Task<IActionResult> Desativar(Guid clienteId, Guid id)
    {
        await _service.DesativarAsync(clienteId, id, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<object?> { Dados = null });
    }
}
