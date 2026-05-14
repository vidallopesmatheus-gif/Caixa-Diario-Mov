using CaixaDiario.API.DTOs.Registros;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/registros")]
[Authorize]
public class RegistrosController : ControllerBase
{
    private readonly RegistroService _registroService;

    public RegistrosController(RegistroService registroService) => _registroService = registroService;

    private Guid ObterUsuarioId() => Guid.Parse(User.FindFirst("id")!.Value);
    private string ObterPerfil() => User.FindFirst("perfil")!.Value;
    private string ObterNomeUsuario() => User.FindFirst("nome_usuario")!.Value;

    [HttpGet("{clienteId:guid}")]
    public async Task<IActionResult> Listar(Guid clienteId)
    {
        var registros = await _registroService.ListarPorClienteAsync(clienteId, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<List<RegistroDto>> { Dados = registros });
    }

    [HttpGet("{clienteId:guid}/{data}")]
    public async Task<IActionResult> ObterPorData(Guid clienteId, DateOnly data)
    {
        var registro = await _registroService.ObterPorDataAsync(clienteId, data, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<RegistroDto> { Dados = registro });
    }

    [HttpPost]
    public async Task<IActionResult> Salvar([FromBody] CriarRegistroDto dto)
    {
        var (resultado, criado) = await _registroService.SalvarAsync(dto, ObterNomeUsuario());
        if (criado)
            return CreatedAtAction(nameof(ObterPorData), new { clienteId = resultado.ClienteId, data = resultado.Data },
                new ApiResponse<RegistroDto> { Dados = resultado });
        return Ok(new ApiResponse<RegistroDto> { Dados = resultado });
    }

    [HttpDelete("{clienteId:guid}/{data}")]
    public async Task<IActionResult> Excluir(Guid clienteId, DateOnly data, [FromBody] ExcluirRegistroDto dto)
    {
        await _registroService.ExcluirAsync(clienteId, data, dto.MotivoExclusao, ObterUsuarioId(), ObterPerfil());
        return Ok(new ApiResponse<object> { Dados = null });
    }
}
