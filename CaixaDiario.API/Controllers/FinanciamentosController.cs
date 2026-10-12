using CaixaDiario.API.DTOs.Financiamentos;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/financiamentos")]
[Authorize]
public class FinanciamentosController : ControllerBase
{
    private readonly IFinanciamentoService _service;

    public FinanciamentosController(IFinanciamentoService service) => _service = service;

    private Guid ObterUsuarioId() => Guid.Parse(User.FindFirst("id")!.Value);
    private string ObterPerfil() => User.FindFirst("perfil")!.Value;

    [HttpGet("{clienteId:guid}")]
    public async Task<IActionResult> Listar(Guid clienteId)
    {
        var resultado = await _service.ListarAsync(clienteId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<List<FinanciamentoDto>> { Dados = resultado });
    }

    [HttpPost("{clienteId:guid}")]
    public async Task<IActionResult> Criar(Guid clienteId, [FromBody] CriarFinanciamentoDto dto)
    {
        var resultado = await _service.CriarAsync(clienteId, ObterUsuarioId(), ObterPerfil(), dto);
        return Ok(new ApiResponse<FinanciamentoDto> { Dados = resultado });
    }

    [HttpGet("{id:guid}/parcelas")]
    public async Task<IActionResult> ListarParcelas(Guid id)
    {
        var resultado = await _service.ListarParcelasAsync(id, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<List<ParcelaFinanciamentoDto>> { Dados = resultado });
    }

    [HttpPost("{id:guid}/parcelas/baixar")]
    public async Task<IActionResult> BaixarParcela(Guid id, [FromBody] BaixarParcelaFinanciamentoDto dto)
    {
        var resultado = await _service.BaixarParcelaAsync(id, ObterUsuarioId(), ObterPerfil(), dto);
        return Ok(new ApiResponse<ParcelaFinanciamentoDto> { Dados = resultado });
    }

    [HttpPost("{id:guid}/parcelas/{parcelaId:guid}/estornar")]
    public async Task<IActionResult> EstornarParcela(Guid id, Guid parcelaId)
    {
        await _service.EstornarParcelaAsync(id, parcelaId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<object> { Dados = null });
    }

    [HttpPost("{id:guid}/adiantar")]
    public async Task<IActionResult> Adiantar(Guid id, [FromBody] AdiantarFinanciamentoDto dto)
    {
        var resultado = await _service.AdiantarAsync(id, ObterUsuarioId(), ObterPerfil(), dto);
        return Ok(new ApiResponse<AdiantamentoResultDto> { Dados = resultado });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, [FromQuery] bool removerAdiantamentos = true)
    {
        await _service.ExcluirAsync(id, ObterUsuarioId(), ObterPerfil(), removerAdiantamentos);
        return Ok(new ApiResponse<object> { Dados = null });
    }
}
