using CaixaDiario.API.DTOs.FaturasCartao;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/faturas-cartao")]
[Authorize]
public class FaturasCartaoController : ControllerBase
{
    private readonly IFaturaCartaoService _service;

    public FaturasCartaoController(IFaturaCartaoService service) => _service = service;

    private Guid ObterUsuarioId() => Guid.Parse(User.FindFirst("id")!.Value);
    private string ObterPerfil() => User.FindFirst("perfil")!.Value;

    [HttpGet("{contaCartaoId:guid}")]
    public async Task<IActionResult> Listar(Guid contaCartaoId)
    {
        var faturas = await _service.ListarFaturasAsync(contaCartaoId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<List<FaturaCartaoDto>> { Dados = faturas });
    }

    [HttpGet("{contaCartaoId:guid}/sugestao")]
    public async Task<IActionResult> Sugerir(Guid contaCartaoId, [FromQuery] decimal valor, [FromQuery] DateOnly data)
    {
        var sugestao = await _service.SugerirFaturaAsync(contaCartaoId, valor, data, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<FaturaCartaoDto?> { Dados = sugestao });
    }

    [HttpPost("vincular-pagamento")]
    public async Task<IActionResult> VincularPagamento([FromBody] VincularPagamentoFaturaDto dto)
    {
        var criado = await _service.VincularPagamentoAsync(dto, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<PagamentoFaturaDto> { Dados = criado });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DesvincularPagamento(Guid id)
    {
        await _service.DesvincularPagamentoAsync(id, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<object> { Dados = null });
    }
}
