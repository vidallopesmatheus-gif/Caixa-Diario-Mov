using CaixaDiario.API.DTOs.Insights;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/insights")]
[Authorize]
public class InsightsController : ControllerBase
{
    private readonly IInsightService _insightService;
    private readonly IRegistroRepository _registroRepo;
    private readonly IContaRecorrenteRepository _contaRecorrenteRepo;
    private readonly IMetaRepository _metaRepo;

    public InsightsController(
        IInsightService insightService,
        IRegistroRepository registroRepo,
        IContaRecorrenteRepository contaRecorrenteRepo,
        IMetaRepository metaRepo)
    {
        _insightService = insightService;
        _registroRepo = registroRepo;
        _contaRecorrenteRepo = contaRecorrenteRepo;
        _metaRepo = metaRepo;
    }

    private Guid ObterUsuarioId() => Guid.Parse(User.FindFirst("id")!.Value);
    private string ObterPerfil() => User.FindFirst("perfil")!.Value;

    [HttpGet("{clienteId:guid}")]
    public async Task<IActionResult> ObterInsights(Guid clienteId)
    {
        if (ObterPerfil() == "cliente" && ObterUsuarioId() != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        var registros   = await _registroRepo.ListarPorClienteAsync(clienteId);
        var recorrentes = await _contaRecorrenteRepo.ListarAtivasPorClienteAsync(clienteId);
        var meta        = await _metaRepo.ObterPorClienteEAnoAsync(clienteId, DateTime.UtcNow.Year);

        var insights = _insightService.Calcular(registros, recorrentes, meta);
        return Ok(new ApiResponse<List<InsightDto>> { Dados = insights });
    }
}
