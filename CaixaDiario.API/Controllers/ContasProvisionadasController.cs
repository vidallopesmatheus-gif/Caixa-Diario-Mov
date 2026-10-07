using CaixaDiario.API.DTOs.Registros;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

// Fase 0.4: CRUD de conta a pagar/receber por Id, sem reenviar o RegistroDiario inteiro.
[ApiController]
[Route("api/contas")]
[Authorize]
public class ContasProvisionadasController : ControllerBase
{
    private readonly IContaProvisionadaService _service;

    public ContasProvisionadasController(IContaProvisionadaService service) => _service = service;

    private Guid ObterUsuarioId() => Guid.Parse(User.FindFirst("id")!.Value);
    private string ObterPerfil() => User.FindFirst("perfil")!.Value;

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarContaProvisionadaDto dto)
    {
        var resultado = await _service.CriarAsync(dto, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<ContaProvisionadaDto> { Dados = resultado });
    }

    [HttpPut("{clienteId:guid}/{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid clienteId, Guid id, [FromBody] AtualizarContaProvisionadaDto dto)
    {
        var resultado = await _service.AtualizarAsync(clienteId, id, dto, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<ContaProvisionadaDto> { Dados = resultado });
    }

    [HttpDelete("{clienteId:guid}/{id:guid}")]
    public async Task<IActionResult> Excluir(Guid clienteId, Guid id)
    {
        await _service.ExcluirAsync(clienteId, id, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<object> { Dados = null });
    }
}
