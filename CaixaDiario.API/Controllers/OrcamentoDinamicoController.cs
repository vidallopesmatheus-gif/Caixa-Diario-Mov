using CaixaDiario.API.DTOs.OrcamentoDinamico;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/orcamento-dinamico")]
[Authorize]
public class OrcamentoDinamicoController : ControllerBase
{
    private readonly IOrcamentoDinamicoService _orcamentoService;
    private readonly IRegistroRepository _registroRepo;
    private readonly IContaRecorrenteRepository _contaRecorrenteRepo;
    private readonly IMetaRepository _metaRepo;

    public OrcamentoDinamicoController(
        IOrcamentoDinamicoService orcamentoService,
        IRegistroRepository registroRepo,
        IContaRecorrenteRepository contaRecorrenteRepo,
        IMetaRepository metaRepo)
    {
        _orcamentoService      = orcamentoService;
        _registroRepo          = registroRepo;
        _contaRecorrenteRepo   = contaRecorrenteRepo;
        _metaRepo              = metaRepo;
    }

    private Guid ObterUsuarioId() => Guid.Parse(User.FindFirst("id")!.Value);
    private string ObterPerfil() => User.FindFirst("perfil")!.Value;

    [HttpGet("{clienteId:guid}")]
    public async Task<IActionResult> ObterOrcamento(Guid clienteId)
    {
        if (ObterPerfil() == "cliente" && ObterUsuarioId() != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        var registros   = await _registroRepo.ListarPorClienteAsync(clienteId);
        var recorrentes = await _contaRecorrenteRepo.ListarAtivasPorClienteAsync(clienteId);
        var metas       = await _metaRepo.ListarPorClienteAsync(clienteId);

        var resultado = _orcamentoService.Calcular(registros, recorrentes, metas);
        return Ok(new ApiResponse<OrcamentoDinamicoDto> { Dados = resultado });
    }
}
