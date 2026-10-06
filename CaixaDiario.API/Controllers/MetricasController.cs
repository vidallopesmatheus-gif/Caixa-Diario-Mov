using CaixaDiario.API.DTOs.Metricas;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/metricas")]
[Authorize]
public class MetricasController : ControllerBase
{
    private readonly IMetricasService _metricasService;
    private readonly IRegistroRepository _registroRepo;
    private readonly ICategoriaRepository _categoriaRepo;
    private readonly IContaBancariaRepository _contaBancariaRepo;

    public MetricasController(
        IMetricasService metricasService,
        IRegistroRepository registroRepo,
        ICategoriaRepository categoriaRepo,
        IContaBancariaRepository contaBancariaRepo)
    {
        _metricasService = metricasService;
        _registroRepo = registroRepo;
        _categoriaRepo = categoriaRepo;
        _contaBancariaRepo = contaBancariaRepo;
    }

    // Mesmo cálculo de /banco: soma o saldo atual de cada conta ATIVA do cliente — nunca o
    // SaldoFinal de "o registro mais recente", que é só uma conta aleatória, não o consolidado.
    private async Task<decimal> CalcularSaldoConsolidadoAsync(Guid clienteId, List<RegistroDiario> registros)
    {
        var contas = await _contaBancariaRepo.ListarPorClienteAsync(clienteId);
        return contas.Where(c => c.Ativa).Sum(c => ContaBancariaService.ObterSaldoAtual(c, registros));
    }

    private Guid ObterUsuarioId() => Guid.Parse(User.FindFirst("id")!.Value);
    private string ObterPerfil() => User.FindFirst("perfil")!.Value;

    private void VerificarAcesso(Guid clienteId)
    {
        if (ObterPerfil() == "cliente" && ObterUsuarioId() != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");
    }

    [HttpGet("{clienteId:guid}")]
    public async Task<IActionResult> ObterMetricas(Guid clienteId, [FromQuery] DateOnly de, [FromQuery] DateOnly ate, [FromQuery] decimal multiplo = 3)
    {
        VerificarAcesso(clienteId);
        var todos = await _registroRepo.ListarPorClienteAsync(clienteId);
        var doPeriodo = todos.Where(r => r.Data >= de && r.Data <= ate).ToList();
        var saldoConsolidado = await CalcularSaldoConsolidadoAsync(clienteId, todos);
        var resultado = _metricasService.CalcularPeriodo(todos, doPeriodo, saldoConsolidado, multiplo);
        return Ok(new ApiResponse<MetricasPeriodoDto> { Dados = resultado });
    }

    [HttpGet("{clienteId:guid}/evolucao")]
    public async Task<IActionResult> ObterEvolucao(Guid clienteId, [FromQuery] int meses = 12)
    {
        VerificarAcesso(clienteId);
        var registros = await _registroRepo.ListarPorClienteAsync(clienteId);
        var resultado = _metricasService.CalcularEvolucao(registros, meses);
        return Ok(new ApiResponse<List<EvolucaoMensalDto>> { Dados = resultado });
    }

    [HttpGet("{clienteId:guid}/dre")]
    public async Task<IActionResult> ObterDre(
        Guid clienteId,
        [FromQuery] DateOnly de,
        [FromQuery] DateOnly ate,
        [FromQuery] Guid? contaBancariaId = null)
    {
        VerificarAcesso(clienteId);
        var todos = await _registroRepo.ListarPorClienteAsync(clienteId);
        var filtrados = todos
            .Where(r => r.Data >= de && r.Data <= ate && !r.Excluido)
            .ToList();

        if (contaBancariaId.HasValue && contaBancariaId.Value != Guid.Empty)
            filtrados = filtrados.Where(r => r.ContaBancariaId == contaBancariaId).ToList();

        var categorias = await _categoriaRepo.ListarTodasAsync();
        var resultado = _metricasService.CalcularDre(filtrados, categorias);

        // Painel de KPIs da tela de DRE — reaproveita o próprio resultado do CalcularDre (não
        // recalcula nada), só acrescenta Ponto de Equilíbrio e a série de Resultado Líquido dos
        // últimos 6 meses ancorada no fim do período selecionado (não em "hoje").
        resultado.PontoEquilibrio = _metricasService.CalcularPontoEquilibrio(
            resultado.ReceitaBruta, resultado.MargemContribuicao, resultado.DespesasFixas.Total, ate);
        resultado.EvolucaoResultadoLiquido = _metricasService.CalcularResultadoLiquidoMensal(todos, ate, categorias);

        return Ok(new ApiResponse<DreDto> { Dados = resultado });
    }

    [HttpGet("{clienteId:guid}/indicadores")]
    public async Task<IActionResult> ObterIndicadores(Guid clienteId, [FromQuery] int mesesEvolucao = 13)
    {
        VerificarAcesso(clienteId);
        var todos = await _registroRepo.ListarPorClienteAsync(clienteId);
        var registros = todos.Where(r => !r.Excluido).ToList();
        var categorias = await _categoriaRepo.ListarTodasAsync();
        var resultado = _metricasService.CalcularIndicadores(registros, mesesEvolucao, categorias);
        return Ok(new ApiResponse<IndicadoresDecisaoDto> { Dados = resultado });
    }
}
