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
    private readonly IDetectorRecorrenciaService _detectorService;

    public ContasRecorrentesController(IContaRecorrenteService service, IDetectorRecorrenciaService detectorService)
    {
        _service = service;
        _detectorService = detectorService;
    }

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
    public async Task<IActionResult> Desativar(Guid clienteId, Guid id, [FromQuery] bool removerPendentes = false)
    {
        await _service.DesativarAsync(clienteId, id, removerPendentes, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<object?> { Dados = null });
    }

    // Item 2 do Bloco 4 ("Visão de Futuro"): padrões detectados no histórico que ainda não são
    // ContaRecorrente — "Criar Contas a Pagar?" na tela.
    [HttpGet("{clienteId:guid}/sugestoes")]
    public async Task<IActionResult> ListarSugestoes(Guid clienteId)
    {
        var resultado = await _detectorService.ListarSugestoesAsync(clienteId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<List<SugestaoRecorrenciaDto>> { Dados = resultado });
    }

    [HttpPost("{clienteId:guid}/sugestoes/ignorar")]
    public async Task<IActionResult> IgnorarSugestao(Guid clienteId, [FromBody] IgnorarSugestaoRecorrenciaDto dto)
    {
        await _detectorService.IgnorarAsync(clienteId, dto.Chave, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<object?> { Dados = null });
    }
}
