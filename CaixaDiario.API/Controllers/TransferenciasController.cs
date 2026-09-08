using CaixaDiario.API.DTOs.Transferencias;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/transferencias")]
[Authorize]
public class TransferenciasController : ControllerBase
{
    private readonly ITransferenciaService _service;

    public TransferenciasController(ITransferenciaService service) => _service = service;

    private Guid ObterUsuarioId() => Guid.Parse(User.FindFirst("id")!.Value);
    private string ObterPerfil() => User.FindFirst("perfil")!.Value;

    [HttpGet("{clienteId:guid}")]
    public async Task<IActionResult> Listar(Guid clienteId)
    {
        var transferencias = await _service.ListarPorClienteAsync(clienteId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<List<TransferenciaDto>> { Dados = transferencias });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DesfazerClassificacao(Guid id)
    {
        await _service.DesfazerClassificacaoAsync(id, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<object> { Dados = null });
    }

    [HttpPost("converter-lancamento")]
    public async Task<IActionResult> ConverterLancamento([FromBody] ConverterLancamentoEmTransferenciaDto dto)
    {
        var criada = await _service.ConverterLancamentoAsync(dto, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<TransferenciaDto> { Dados = criada });
    }
}
