using CaixaDiario.API.DTOs.ContasBancarias;
using CaixaDiario.API.DTOs.Importacao;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
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
    private readonly IDuplicataExtratoService _duplicataExtratoService;

    public ContasBancariasController(
        IContaBancariaService service, IImportacaoService importacaoService, IDuplicataExtratoService duplicataExtratoService)
    {
        _service = service;
        _importacaoService = importacaoService;
        _duplicataExtratoService = duplicataExtratoService;
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
    public async Task<IActionResult> ExcluirOuInativar(Guid id)
    {
        var resultado = await _service.ExcluirOuInativarAsync(id, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<ExclusaoContaBancariaResultDto> { Dados = resultado });
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
        [FromForm] DateOnly? dataInicio, [FromForm] DateOnly? dataFim,
        // Fase 1.7: JSON [{"transacaoIndice":N,"acao":"Mesclar"|"ImportarComoNovo"}] — só pras
        // transações que a tela de revisão listou em DuplicatasManuais; índice ausente = Mesclar.
        [FromForm] string? resolucoesDuplicatasJson = null)
    {
        List<ResolucaoDuplicataDto>? resolucoes = null;
        if (!string.IsNullOrWhiteSpace(resolucoesDuplicatasJson))
        {
            try
            {
                resolucoes = System.Text.Json.JsonSerializer.Deserialize<List<ResolucaoDuplicataDto>>(
                    resolucoesDuplicatasJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (System.Text.Json.JsonException)
            {
                throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS,
                    "resolucoesDuplicatasJson inválido.", "resolucoesDuplicatasJson");
            }
        }

        var resultado = await _importacaoService.ImportarArquivoAsync(
            contaId, ObterUsuarioId(), ObterPerfil(), arquivo, dataInicio, dataFim, resolucoes);
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

    [HttpPost("{contaId:guid}/excluir-lancamento")]
    public async Task<IActionResult> ExcluirLancamento(Guid contaId, [FromBody] ExcluirLancamentoDto dto)
    {
        var resultado = await _importacaoService.ExcluirLancamentoAsync(contaId, ObterUsuarioId(), ObterPerfil(), dto);
        return Ok(new ApiResponse<ExcluirLancamentoResultDto> { Dados = resultado });
    }

    // Fase 0.5: "Revisar duplicatas" — só sugere pares, a exclusão usa o endpoint acima.
    [HttpGet("{contaId:guid}/duplicatas")]
    public async Task<IActionResult> ListarDuplicatas(Guid contaId)
    {
        var resultado = await _duplicataExtratoService.ListarProvaveisAsync(contaId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<List<DuplicataProvavelDto>> { Dados = resultado });
    }

    // Item 3.2: "Manter os dois" — grava a decisão pra esse par não ser sugerido de novo.
    [HttpPost("{contaId:guid}/duplicatas/manter")]
    public async Task<IActionResult> ManterDuplicata(Guid contaId, [FromBody] ManterDuplicataDto dto)
    {
        await _duplicataExtratoService.ManterOsDoisAsync(contaId, dto.LancamentoAId, dto.LancamentoBId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<object> { Dados = null });
    }
}
