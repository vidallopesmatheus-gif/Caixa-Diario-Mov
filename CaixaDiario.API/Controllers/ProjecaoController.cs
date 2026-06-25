using CaixaDiario.API.DTOs.Projecao;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/projecao")]
[Authorize]
public class ProjecaoController : ControllerBase
{
    private readonly IProjecaoService _projecaoService;
    private readonly IRegistroRepository _registroRepo;
    private readonly IContaRecorrenteRepository _recorrenteRepo;

    public ProjecaoController(
        IProjecaoService projecaoService,
        IRegistroRepository registroRepo,
        IContaRecorrenteRepository recorrenteRepo)
    {
        _projecaoService  = projecaoService;
        _registroRepo     = registroRepo;
        _recorrenteRepo   = recorrenteRepo;
    }

    private Guid ObterUsuarioId() => Guid.Parse(User.FindFirst("id")!.Value);
    private string ObterPerfil()  => User.FindFirst("perfil")!.Value;

    [HttpGet("{clienteId:guid}")]
    public async Task<IActionResult> ObterProjecao(
        Guid clienteId,
        [FromQuery] int dias = 30,
        [FromQuery] Guid? contaBancariaId = null)
    {
        if (ObterPerfil() == "cliente" && ObterUsuarioId() != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        if (dias is not (30 or 60 or 90))
            dias = 30;

        var registros   = await _registroRepo.ListarPorClienteAsync(clienteId);
        var recorrentes = await _recorrenteRepo.ListarAtivasPorClienteAsync(clienteId);

        var resultado = _projecaoService.Calcular(registros, recorrentes, dias, contaBancariaId);
        return Ok(new ApiResponse<ProjecaoDto> { Dados = resultado });
    }
}
