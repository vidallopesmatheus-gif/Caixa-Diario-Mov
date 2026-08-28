using CaixaDiario.API.DTOs.SaudeFinanceira;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/saude-financeira")]
[Authorize]
public class SaudeFinanceiraController : ControllerBase
{
    private readonly ISaudeFinanceiraService _saudeService;
    private readonly IRegistroRepository _registroRepo;
    private readonly IContaRecorrenteRepository _contaRecorrenteRepo;
    private readonly IMetaRepository _metaRepo;
    private readonly IMetaProgressoService _metaProgressoService;

    public SaudeFinanceiraController(
        ISaudeFinanceiraService saudeService,
        IRegistroRepository registroRepo,
        IContaRecorrenteRepository contaRecorrenteRepo,
        IMetaRepository metaRepo,
        IMetaProgressoService metaProgressoService)
    {
        _saudeService        = saudeService;
        _registroRepo        = registroRepo;
        _contaRecorrenteRepo = contaRecorrenteRepo;
        _metaRepo            = metaRepo;
        _metaProgressoService = metaProgressoService;
    }

    private Guid ObterUsuarioId() => Guid.Parse(User.FindFirst("id")!.Value);
    private string ObterPerfil()  => User.FindFirst("perfil")!.Value;

    [HttpGet("{clienteId:guid}")]
    public async Task<IActionResult> ObterSaudeFinanceira(Guid clienteId)
    {
        if (ObterPerfil() == "cliente" && ObterUsuarioId() != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        var registros   = await _registroRepo.ListarPorClienteAsync(clienteId);
        var recorrentes = await _contaRecorrenteRepo.ListarAtivasPorClienteAsync(clienteId);
        var metas       = await _metaRepo.ListarPorClienteAsync(clienteId);
        await _metaProgressoService.AplicarSaldoDeContasVinculadasAsync(clienteId, metas);

        var resultado = _saudeService.Calcular(registros, recorrentes, metas);
        return Ok(new ApiResponse<SaudeFinanceiraDto> { Dados = resultado });
    }
}
