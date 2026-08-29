using CaixaDiario.API.DTOs.ContasBancarias;
using CaixaDiario.API.DTOs.Importacao;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/contas-bancarias")]
[Authorize]
public class ContasBancariasController : ControllerBase
{
    private readonly IContaBancariaService _service;
    private readonly IImportacaoService _importacaoService;

    public ContasBancariasController(IContaBancariaService service, IImportacaoService importacaoService)
    {
        _service = service;
        _importacaoService = importacaoService;
    }

    private Guid ObterUsuarioId() => Guid.Parse(User.FindFirst("id")!.Value);
    private string ObterPerfil() => User.FindFirst("perfil")!.Value;

    [HttpGet("{clienteId:guid}")]
    public async Task<IActionResult> Listar(Guid clienteId)
    {
        var contas = await _service.ListarPorClienteAsync(clienteId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<List<ContaBancariaDto>> { Dados = contas });
    }

    [HttpGet("detalhe/{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id)
    {
        var conta = await _service.ObterPorIdAsync(id, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<ContaBancariaDto> { Dados = conta });
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarContaBancariaDto dto)
    {
        var criada = await _service.CriarAsync(dto, ObterUsuarioId(), ObterPerfil());
        return CreatedAtAction(nameof(ObterPorId), new { id = criada.Id },
            new ApiResponse<ContaBancariaDto> { Dados = criada });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] AtualizarContaBancariaDto dto)
    {
        var atualizada = await _service.AtualizarAsync(id, dto, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<ContaBancariaDto> { Dados = atualizada });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Inativar(Guid id)
    {
        await _service.InativarAsync(id, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<object> { Dados = null });
    }

    // ── Extrato e pendências por conta ─────────────────────────────────────────

    [HttpGet("{contaId:guid}/extrato")]
    public async Task<IActionResult> ObterExtrato(Guid contaId, [FromQuery] DateOnly? de, [FromQuery] DateOnly? ate)
    {
        var lancamentos = await _service.ObterExtratoAsync(contaId, ObterUsuarioId(), ObterPerfil(), de, ate);
        return Ok(new ApiResponse<List<LancamentoExtratoDto>> { Dados = lancamentos });
    }

    [HttpGet("{contaId:guid}/pendencias")]
    public async Task<IActionResult> ObterPendencias(Guid contaId)
    {
        var pendencias = await _service.ObterPendenciasAsync(contaId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<PendenciasContaDto> { Dados = pendencias });
    }

    // ── Investimento: rendimento e vínculo com meta ────────────────────────────

    [HttpPost("{contaId:guid}/rendimento")]
    public async Task<IActionResult> RegistrarRendimento(Guid contaId, [FromBody] RegistrarRendimentoDto dto)
    {
        var conta = await _service.RegistrarRendimentoAsync(contaId, dto, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<ContaBancariaDto> { Dados = conta });
    }

    [HttpPost("{contaId:guid}/vincular-meta/{metaId:guid}")]
    public async Task<IActionResult> VincularMeta(Guid contaId, Guid metaId)
    {
        var conta = await _service.VincularMetaAsync(contaId, metaId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<ContaBancariaDto> { Dados = conta });
    }

    [HttpPost("{contaId:guid}/desvincular-meta/{metaId:guid}")]
    public async Task<IActionResult> DesvincularMeta(Guid contaId, Guid metaId)
    {
        var conta = await _service.DesvincularMetaAsync(contaId, metaId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<ContaBancariaDto> { Dados = conta });
    }

    // ── Importação de extrato ──────────────────────────────────────────────────

    [HttpPost("{contaId:guid}/preview-extrato")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    public async Task<IActionResult> PreviewExtrato(
        Guid contaId, IFormFile arquivo,
        [FromForm] DateOnly? dataInicio, [FromForm] DateOnly? dataFim)
    {
        var preview = await _importacaoService.PreviewAsync(contaId, ObterUsuarioId(), ObterPerfil(), arquivo, dataInicio, dataFim);
        return Ok(new ApiResponse<PreviewImportacaoDto> { Dados = preview });
    }

    [HttpPost("{contaId:guid}/importar-extrato")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    public async Task<IActionResult> ImportarExtrato(
        Guid contaId, IFormFile arquivo,
        [FromForm] DateOnly? dataInicio, [FromForm] DateOnly? dataFim)
    {
        var resultado = await _importacaoService.ImportarArquivoAsync(
            contaId, ObterUsuarioId(), ObterPerfil(), arquivo, dataInicio, dataFim);
        return Ok(new ApiResponse<ResultadoImportacaoDto> { Dados = resultado });
    }

    [HttpGet("{contaId:guid}/pendentes-categorizacao")]
    public async Task<IActionResult> ListarPendentesCategorizacao(Guid contaId)
    {
        var pendentes = await _importacaoService.ListarPendentesCategorizacaoAsync(
            contaId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<List<PendenteCategorizacaoDto>> { Dados = pendentes });
    }

    [HttpPost("{contaId:guid}/categorizar-pendentes")]
    public async Task<IActionResult> CategorizarPendentes(Guid contaId, [FromBody] AtualizarCategoriaDto dto)
    {
        await _importacaoService.AtualizarCategoriasAsync(contaId, ObterUsuarioId(), ObterPerfil(), dto);
        return Ok(new ApiResponse<object> { Dados = null });
    }
}
