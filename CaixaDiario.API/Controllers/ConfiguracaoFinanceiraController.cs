using CaixaDiario.API.DTOs.ConfiguracaoFinanceira;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/configuracao-financeira")]
[Authorize]
public class ConfiguracaoFinanceiraController : ControllerBase
{
    private readonly IConfiguracaoFinanceiraService _service;

    public ConfiguracaoFinanceiraController(IConfiguracaoFinanceiraService service) => _service = service;

    private Guid ObterUsuarioId() => Guid.Parse(User.FindFirst("id")!.Value);
    private string ObterPerfil() => User.FindFirst("perfil")!.Value;

    [HttpGet("{clienteId:guid}")]
    public async Task<IActionResult> Obter(Guid clienteId)
    {
        var dados = await _service.ObterAsync(clienteId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<ConfiguracaoFinanceiraDto> { Dados = dados });
    }

    [HttpPut("{clienteId:guid}")]
    public async Task<IActionResult> Atualizar(Guid clienteId, [FromBody] AtualizarConfiguracaoFinanceiraDto dto)
    {
        var dados = await _service.AtualizarAsync(clienteId, dto, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<ConfiguracaoFinanceiraDto> { Dados = dados });
    }
}
