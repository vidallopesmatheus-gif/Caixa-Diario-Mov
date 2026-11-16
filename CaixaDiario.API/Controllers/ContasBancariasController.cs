using CaixaDiario.API.DTOs.ConciliacaoSaldo;
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
    private readonly IConciliacaoSaldoService _conciliacaoSaldoService;
    private readonly IPeriodoConferidoService? _periodoConferido;

    public ContasBancariasController(
        IContaBancariaService service, IImportacaoService importacaoService, IDuplicataExtratoService duplicataExtratoService,
        IConciliacaoSaldoService conciliacaoSaldoService, IPeriodoConferidoService? periodoConferido = null)
    {
        _periodoConferido = periodoConferido;
        _service = service;
        _importacaoService = importacaoService;
        _duplicataExtratoService = duplicataExtratoService;
        _conciliacaoSaldoService = conciliacaoSaldoService;
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

    // "Não é rendimento — foi acerto de saldo": tira a diferença do "Atualizar saldo" da receita do DRE.
    [HttpPost("{contaId:guid}/rendimento/{lancamentoId:guid}/converter-em-ajuste")]
    public async Task<IActionResult> ConverterRendimentoEmAjuste(Guid contaId, Guid lancamentoId, [FromQuery] DateOnly data)
    {
        await _service.ConverterRendimentoEmAjusteAsync(contaId, data, lancamentoId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<object> { Dados = null });
    }

    // Item 4 (Money Vision "saldo não fecha com o extrato") — "Conferir saldo": acerta o saldo da
    // conta com o banco através de um lançamento visível, nunca uma troca silenciosa do Inicio.
    [HttpPost("{contaId:guid}/ajustar-saldo")]
    public async Task<IActionResult> AjustarSaldo(Guid contaId, [FromBody] AjustarSaldoDto dto)
    {
        var conta = await _service.AjustarSaldoAsync(contaId, dto, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<ContaBancariaDto> { Dados = conta });
    }

    // Money Vision ("até onde está lançado"): dias cobertos por extrato, pontos de conciliação com o
    // banco e saldo de cada dia — linha do tempo no topo do extrato.
    [HttpGet("{contaId:guid}/cobertura")]
    public async Task<IActionResult> ObterCobertura(Guid contaId)
    {
        var resultado = await _conciliacaoSaldoService.ObterCoberturaAsync(contaId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<CoberturaExtratoDto> { Dados = resultado });
    }

    // Diagnóstico da diferença app × banco numa data — antes de qualquer ajuste. Sem saldoBanco,
    // usa o do ponto de conciliação já registrado naquela data.
    [HttpGet("{contaId:guid}/diagnostico-saldo")]
    public async Task<IActionResult> DiagnosticarSaldo(Guid contaId, [FromQuery] DateOnly data, [FromQuery] decimal? saldoBanco)
    {
        var resultado = await _conciliacaoSaldoService.DiagnosticarAsync(contaId, data, saldoBanco, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<DiagnosticoDiferencaDto> { Dados = resultado });
    }

    // "Conferir saldo": ajustes de saldo da conta (inclusive os da migração) pra confirmar ou
    // desfazer — desfazer é a exclusão comum do lançamento (excluir-lancamento).
    [HttpGet("{contaId:guid}/ajustes-saldo")]
    public async Task<IActionResult> ListarAjustesSaldo(Guid contaId)
    {
        var resultado = await _conciliacaoSaldoService.ListarAjustesAsync(contaId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<List<AjusteSaldoDto>> { Dados = resultado });
    }

    [HttpPost("{contaId:guid}/ajustes-saldo/{lancamentoId:guid}/confirmar")]
    public async Task<IActionResult> ConfirmarAjusteSaldo(Guid contaId, Guid lancamentoId)
    {
        await _conciliacaoSaldoService.ConfirmarAjusteAsync(contaId, lancamentoId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<object> { Dados = null });
    }

    // "Saldo no banco em 07/11" informado à mão — grava o ponto de conciliação (nunca cria ajuste).
    [HttpPost("{contaId:guid}/saldo-banco")]
    public async Task<IActionResult> RegistrarSaldoBanco(Guid contaId, [FromBody] RegistrarSaldoBancoDto dto)
    {
        var resultado = await _conciliacaoSaldoService.RegistrarSaldoBancoAsync(contaId, dto, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<PontoConciliacaoDto> { Dados = resultado });
    }

    // Money Vision ("travar o período até o saldo conferido"): reabre a partir de uma data (ou tudo).
    [HttpPost("{contaId:guid}/reabrir")]
    public async Task<IActionResult> ReabrirPeriodo(Guid contaId, [FromBody] ReabrirPeriodoDto dto)
    {
        var resultado = await PeriodoConferidoService().ReabrirAsync(contaId, dto, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<TravaPeriodoDto> { Dados = resultado });
    }

    // Histórico de conferências e reaberturas da conta (auditoria).
    [HttpGet("{contaId:guid}/conferencias")]
    public async Task<IActionResult> ListarConferencias(Guid contaId)
    {
        var resultado = await PeriodoConferidoService().ListarHistoricoAsync(contaId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<List<ConferenciaSaldoDto>> { Dados = resultado });
    }

    private IPeriodoConferidoService PeriodoConferidoService() =>
        _periodoConferido ?? throw new InvalidOperationException("IPeriodoConferidoService não registrado.");

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

    // Money Vision — Bloco 5, item 3: gráfico "saldo mês a mês" da conta de investimento.
    [HttpGet("{contaId:guid}/historico-saldo-mensal")]
    public async Task<IActionResult> ObterHistoricoSaldoMensal(Guid contaId, [FromQuery] int meses = 12)
    {
        var historico = await _service.ObterHistoricoSaldoMensalAsync(contaId, ObterUsuarioId(), ObterPerfil(), meses);
        return Ok(new ApiResponse<List<SaldoMensalDto>> { Dados = historico });
    }

    // ── Importação de extrato ──────────────────────────────────────────────────

    [HttpPost("{contaId:guid}/preview-extrato")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    public async Task<IActionResult> PreviewExtrato(
        Guid contaId, IFormFile arquivo,
        [FromForm] DateOnly? dataInicio, [FromForm] DateOnly? dataFim,
        // Escape hatch pra conta CartaoCredito quando a detecção automática de sinal erra o
        // formato do arquivo — ver ImportacaoService.NormalizarSinalParaCartao.
        [FromForm] bool? inverterSinaisManualmente = null)
    {
        var preview = await _importacaoService.PreviewAsync(
            contaId, ObterUsuarioId(), ObterPerfil(), arquivo, dataInicio, dataFim, inverterSinaisManualmente);
        return Ok(new ApiResponse<PreviewImportacaoDto> { Dados = preview });
    }

    [HttpPost("{contaId:guid}/importar-extrato")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    public async Task<IActionResult> ImportarExtrato(
        Guid contaId, IFormFile arquivo,
        [FromForm] DateOnly? dataInicio, [FromForm] DateOnly? dataFim,
        // Fase 1.7: JSON [{"transacaoIndice":N,"acao":"Mesclar"|"ImportarComoNovo"}] — só pras
        // transações que a tela de revisão listou em DuplicatasManuais; índice ausente = Mesclar.
        [FromForm] string? resolucoesDuplicatasJson = null,
        // Cartão (Bloco 2): obrigatórios só quando a conta é CartaoCredito — ver ImportacaoService.
        [FromForm] string? banco = null, [FromForm] DateOnly? dataVencimentoFatura = null,
        [FromForm] DateOnly? dataFechamentoFatura = null, [FromForm] string? competenciaFatura = null,
        [FromForm] bool? inverterSinaisManualmente = null,
        // Money Vision (conciliação): JSON [{"id":"...","data":"yyyy-MM-dd"}] dos lançamentos que
        // sumiram do extrato novo e o usuário mandou excluir; e o saldo do banco digitado na prévia
        // (texto com ponto decimal — nunca depende da cultura do servidor).
        [FromForm] string? excluirAusentesJson = null,
        [FromForm] string? saldoBancoInformado = null, [FromForm] DateOnly? dataSaldoBanco = null)
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

        var opcoesConciliacao = new OpcoesConciliacaoImportacaoDto { DataSaldoBanco = dataSaldoBanco };
        if (!string.IsNullOrWhiteSpace(excluirAusentesJson))
        {
            try
            {
                opcoesConciliacao.ExcluirAusentes = System.Text.Json.JsonSerializer.Deserialize<List<ItemExcluirLoteDto>>(
                    excluirAusentesJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }
            catch (System.Text.Json.JsonException)
            {
                throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "excluirAusentesJson inválido.", "excluirAusentesJson");
            }
        }
        if (!string.IsNullOrWhiteSpace(saldoBancoInformado))
        {
            if (!decimal.TryParse(saldoBancoInformado, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var saldo))
                throw new ApiException(400, CodigoRetorno.DADOS_INVALIDOS, "Saldo do banco inválido.", "saldoBancoInformado");
            opcoesConciliacao.SaldoBancoInformado = saldo;
        }

        var resultado = await _importacaoService.ImportarArquivoAsync(
            contaId, ObterUsuarioId(), ObterPerfil(), arquivo, dataInicio, dataFim, resolucoes,
            banco, dataVencimentoFatura, dataFechamentoFatura, competenciaFatura, inverterSinaisManualmente,
            opcoesConciliacao);
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

    // Money Vision: "excluir em massa" — um endpoint só, numa transação só, em vez do frontend
    // fazer N chamadas em sequência pro endpoint acima.
    [HttpPost("{contaId:guid}/excluir-lote")]
    public async Task<IActionResult> ExcluirLote(Guid contaId, [FromBody] ExcluirLoteDto dto)
    {
        var resultado = await _importacaoService.ExcluirLoteAsync(contaId, ObterUsuarioId(), ObterPerfil(), dto);
        return Ok(new ApiResponse<ExcluirLoteResultDto> { Dados = resultado });
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
