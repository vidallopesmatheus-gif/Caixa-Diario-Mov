using CaixaDiario.API.DTOs.Admin;
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

    public AdminMigracaoController(
        IMigracaoRecorrenciasService migracaoRecorrenciasService,
        IReclassificacaoCategoriasService reclassificacaoCategoriasService)
    {
        _migracaoRecorrenciasService = migracaoRecorrenciasService;
        _reclassificacaoCategoriasService = reclassificacaoCategoriasService;
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
}
