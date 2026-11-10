using CaixaDiario.API.DTOs.Admin;
using CaixaDiario.API.DTOs.Transferencias;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/admin/migracao")]
[Authorize]
public class AdminMigracaoController : ControllerBase
{
    private readonly IMigracaoRecorrenciasService _migracaoRecorrenciasService;
    private readonly IReclassificacaoCategoriasService _reclassificacaoCategoriasService;
    private readonly ILimpezaTransferenciasService _limpezaTransferenciasService;
    private readonly IRecalculoSaldoService _recalculoSaldoService;
    private readonly IVerificacaoSinalTransferenciaService _verificacaoSinalTransferenciaService;
    private readonly ICorrigirDataParcelasService _corrigirDataParcelasService;
    private readonly ICorrigirFaturasService _corrigirFaturasService;

    public AdminMigracaoController(
        IMigracaoRecorrenciasService migracaoRecorrenciasService,
        IReclassificacaoCategoriasService reclassificacaoCategoriasService,
        ILimpezaTransferenciasService limpezaTransferenciasService,
        IRecalculoSaldoService recalculoSaldoService,
        IVerificacaoSinalTransferenciaService verificacaoSinalTransferenciaService,
        ICorrigirDataParcelasService corrigirDataParcelasService,
        ICorrigirFaturasService corrigirFaturasService)
    {
        _migracaoRecorrenciasService = migracaoRecorrenciasService;
        _reclassificacaoCategoriasService = reclassificacaoCategoriasService;
        _limpezaTransferenciasService = limpezaTransferenciasService;
        _recalculoSaldoService = recalculoSaldoService;
        _verificacaoSinalTransferenciaService = verificacaoSinalTransferenciaService;
        _corrigirDataParcelasService = corrigirDataParcelasService;
        _corrigirFaturasService = corrigirFaturasService;
    }

    private void VerificarAdmin()
    {
        if (User.FindFirst("perfil")?.Value != "admin")
            throw new ApiException(403, CodigoRetorno.SEM_PERMISSAO, "Acesso restrito a administradores.");
    }

    // Sem "confirmar" (ou confirmar=false): só relatório, nada é gravado. Rodar assim primeiro,
    // revisar o relatório, só então repetir com ?confirmar=true pra aplicar de verdade.
    [HttpPost("recorrencias-sem-conta-correta")]
    public async Task<IActionResult> MigrarRecorrenciasSemContaCorreta([FromQuery] bool confirmar = false)
    {
        VerificarAdmin();
        var resultado = await _migracaoRecorrenciasService.MigrarOcorrenciasSemContaCorretaAsync(confirmar);
        return Ok(new ApiResponse<MigracaoRecorrenciasResultDto> { Dados = resultado });
    }

    // Item 8 da rodada de ajustes Money Vision: "migrar categorias existentes por padrão de uso,
    // gerando um relatório antes de aplicar". Sem "confirmar" (ou confirmar=false): só relatório,
    // nada é gravado. Rodar assim primeiro, revisar o relatório, só então repetir com
    // ?confirmar=true pra aplicar de verdade.
    [HttpPost("categorias-reclassificar-por-uso")]
    public async Task<IActionResult> ReclassificarCategoriasPorUso([FromQuery] bool confirmar = false)
    {
        VerificarAdmin();
        var resultado = await _reclassificacaoCategoriasService.ReclassificarPorUsoAsync(confirmar);
        return Ok(new ApiResponse<ReclassificacaoCategoriasResultDto> { Dados = resultado });
    }

    // Correção do bug "desfazer transferência não remove a contrapartida nem recalcula o saldo":
    // limpa contrapartidas provisórias órfãs (criadas do zero, nunca confirmadas pelo extrato real,
    // cuja Transferência já foi desfeita) e recalcula a cadeia de saldo das contas afetadas, do
    // zero. Sem "confirmar" (ou confirmar=false): só relatório, nada é gravado.
    [HttpPost("contrapartidas-orfas-limpar")]
    public async Task<IActionResult> LimparContrapartidasOrfas([FromQuery] bool confirmar = false)
    {
        VerificarAdmin();
        var resultado = await _limpezaTransferenciasService.LimparContrapartidasOrfasAsync(confirmar);
        return Ok(new ApiResponse<LimpezaContrapartidasOrfasResultDto> { Dados = resultado });
    }

    // Money Vision: "saldo das contas não fecha com o extrato" — repara a cadeia de Inicio/
    // SaldoFinal de TODAS as contas de TODOS os clientes (a corrupção histórica que já existia
    // antes dos write paths passarem a cascatear certo). Sem "confirmar" (ou confirmar=false): só
    // relatório, nada é gravado.
    [HttpPost("saldos-recalcular")]
    public async Task<IActionResult> RecalcularSaldos([FromQuery] bool confirmar = false)
    {
        VerificarAdmin();
        var resultado = await _recalculoSaldoService.RecalcularTodasAsContasAsync(confirmar);
        return Ok(new ApiResponse<RecalculoSaldoResultDto> { Dados = resultado });
    }

    // Money Vision ("ajuste mexeu no DRE"): converte os ajustes de saldo antigos pro tipo de sistema
    // AjusteConciliacao (fora do DRE e dos indicadores). Sem "confirmar": só relatório.
    [HttpPost("ajustes-conciliacao-migrar")]
    public async Task<IActionResult> MigrarAjustesConciliacao([FromQuery] bool confirmar = false)
    {
        VerificarAdmin();
        var resultado = await _recalculoSaldoService.MigrarAjustesConciliacaoAsync(confirmar);
        return Ok(new ApiResponse<MigracaoAjustesConciliacaoResultDto> { Dados = resultado });
    }

    // Money Vision ("ajustes da migração que o usuário não entende"): par de ajuste que se anula
    // sai, par 30/09 → 01/10 vira um ajuste líquido com descrição clara. Sem "confirmar": só relatório.
    [HttpPost("ajustes-saldo-consolidar")]
    public async Task<IActionResult> ConsolidarAjustesSaldo([FromQuery] bool confirmar = false)
    {
        VerificarAdmin();
        var resultado = await _recalculoSaldoService.ConsolidarAjustesAsync(confirmar);
        return Ok(new ApiResponse<ConsolidacaoAjustesResultDto> { Dados = resultado });
    }

    // Money Vision: "Pix recebido entrando como saída" (item 3) — só relatório, nunca corrige
    // sozinho (ver VerificacaoSinalTransferenciaService).
    [HttpGet("transferencias-sinal-suspeito")]
    public async Task<IActionResult> ListarTransferenciasComSinalSuspeito()
    {
        VerificarAdmin();
        var resultado = await _verificacaoSinalTransferenciaService.ListarSinaisSuspeitosAsync();
        return Ok(new ApiResponse<List<TransferenciaSinalSuspeitoDto>> { Dados = resultado });
    }

    // Money Vision: "data da parcela vs. competência" (item 2) — repara parcelas importadas antes
    // da reancoragem existir. Sem "confirmar" (ou confirmar=false): só relatório, nada é gravado.
    [HttpPost("parcelas-corrigir-data")]
    public async Task<IActionResult> CorrigirDataParcelas([FromQuery] bool confirmar = false)
    {
        VerificarAdmin();
        var resultado = await _corrigirDataParcelasService.CorrigirAsync(confirmar);
        return Ok(new ApiResponse<CorrigirDataParcelasResultDto> { Dados = resultado });
    }

    // Money Vision: "fatura do C6 dividida em duas + pagamentos não vinculados" (item 3).
    [HttpPost("faturas-corrigir")]
    public async Task<IActionResult> CorrigirFaturas([FromQuery] bool confirmar = false)
    {
        VerificarAdmin();
        var resultado = await _corrigirFaturasService.CorrigirAsync(confirmar);
        return Ok(new ApiResponse<CorrigirFaturasResultDto> { Dados = resultado });
    }
}
