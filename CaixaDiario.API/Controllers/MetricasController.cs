using CaixaDiario.API.DTOs.Metricas;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
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
    private readonly IContaRecorrenteRepository _contaRecorrenteRepo;

    public MetricasController(IMetricasService metricasService, IRegistroRepository registroRepo, IContaRecorrenteRepository contaRecorrenteRepo)
    {
        _metricasService = metricasService;
        _registroRepo = registroRepo;
        _contaRecorrenteRepo = contaRecorrenteRepo;
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
        var resultado = _metricasService.CalcularPeriodo(todos, doPeriodo, multiplo);
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

    [HttpGet("{clienteId:guid}/fluxo-projetado")]
    public async Task<IActionResult> ObterFluxoProjetado(Guid clienteId, [FromQuery] int dias = 90)
    {
        VerificarAcesso(clienteId);
        var registros = await _registroRepo.ListarPorClienteAsync(clienteId);
        var recorrentes = await _contaRecorrenteRepo.ListarAtivasPorClienteAsync(clienteId);
        var resultado = _metricasService.CalcularFluxoProjetado(registros, recorrentes, dias);
        return Ok(new ApiResponse<FluxoProjetadoDto> { Dados = resultado });
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

        var resultado = _metricasService.CalcularDre(filtrados);
        return Ok(new ApiResponse<DreDto> { Dados = resultado });
    }

    [HttpGet("{clienteId:guid}/indicadores")]
    public async Task<IActionResult> ObterIndicadores(Guid clienteId, [FromQuery] int mesesEvolucao = 13)
    {
        VerificarAcesso(clienteId);
        var todos = await _registroRepo.ListarPorClienteAsync(clienteId);
        var registros = todos.Where(r => !r.Excluido).ToList();
        var resultado = _metricasService.CalcularIndicadores(registros, mesesEvolucao);
        return Ok(new ApiResponse<IndicadoresDecisaoDto> { Dados = resultado });
    }
}
