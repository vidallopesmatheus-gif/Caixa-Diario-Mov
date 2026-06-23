using CaixaDiario.API.DTOs.Auditoria;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/auditoria")]
[Authorize]
public class AuditoriaController : ControllerBase
{
    private readonly IAuditRepository _repo;

    public AuditoriaController(IAuditRepository repo) => _repo = repo;

    private Guid ObterUsuarioId() => Guid.Parse(User.FindFirst("id")!.Value);
    private string ObterPerfil() => User.FindFirst("perfil")!.Value;

    [HttpGet("{clienteId:guid}")]
    public async Task<IActionResult> Listar(
        Guid clienteId,
        [FromQuery] DateTime? de,
        [FromQuery] DateTime? ate,
        [FromQuery] string? entidade,
        [FromQuery] string? acao,
        [FromQuery] int pagina = 1)
    {
        if (ObterPerfil() == "cliente" && ObterUsuarioId() != clienteId)
            throw new ApiException(403, CodigoRetorno.ACESSO_NEGADO, "Acesso negado.");

        const int tamanhoPagina = 50;
        var (items, total) = await _repo.ListarPaginadoAsync(clienteId, de, ate, entidade, acao, pagina, tamanhoPagina);

        var resultado = new AuditLogPaginadoDto
        {
            Items = items.Select(l => new AuditLogDto
            {
                Id = l.Id, ClienteId = l.ClienteId, UsuarioId = l.UsuarioId,
                Entidade = l.Entidade, AcaoTipo = l.AcaoTipo, EntidadeId = l.EntidadeId,
                DadosAntes = l.DadosAntes, DadosDepois = l.DadosDepois, OcorridoEm = l.OcorridoEm,
            }).ToList(),
            Total = total, Pagina = pagina, TamanhoPagina = tamanhoPagina,
        };

        return Ok(new ApiResponse<AuditLogPaginadoDto> { Dados = resultado });
    }
}
