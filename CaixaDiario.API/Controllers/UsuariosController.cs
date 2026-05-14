using CaixaDiario.API.DTOs.Usuarios;
using CaixaDiario.API.Enums;
using CaixaDiario.API.Exceptions;
using CaixaDiario.API.Responses;
using CaixaDiario.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaixaDiario.API.Controllers;

[ApiController]
[Route("api/usuarios")]
[Authorize]
public class UsuariosController : ControllerBase
{
    private readonly UsuarioService _usuarioService;

    public UsuariosController(UsuarioService usuarioService) => _usuarioService = usuarioService;

    private void VerificarAdmin()
    {
        if (User.FindFirst("perfil")?.Value != "admin")
            throw new ApiException(403, CodigoRetorno.SEM_PERMISSAO, "Acesso restrito a administradores.");
    }

    private string ObterNomeUsuario() => User.FindFirst("nome_usuario")!.Value;

    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        VerificarAdmin();
        var usuarios = await _usuarioService.ListarAsync();
        return Ok(new ApiResponse<List<UsuarioDto>> { Dados = usuarios });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id)
    {
        VerificarAdmin();
        var usuario = await _usuarioService.ObterPorIdAsync(id);
        return Ok(new ApiResponse<UsuarioDto> { Dados = usuario });
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarUsuarioDto dto)
    {
        VerificarAdmin();
        var usuario = await _usuarioService.CriarAsync(dto, ObterNomeUsuario());
        return CreatedAtAction(nameof(ObterPorId), new { id = usuario.Id }, new ApiResponse<UsuarioDto> { Dados = usuario });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] AtualizarUsuarioDto dto)
    {
        VerificarAdmin();
        var usuario = await _usuarioService.AtualizarAsync(id, dto, ObterNomeUsuario());
        return Ok(new ApiResponse<UsuarioDto> { Dados = usuario });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Desativar(Guid id)
    {
        VerificarAdmin();
        await _usuarioService.DesativarAsync(id, ObterNomeUsuario());
        return Ok(new ApiResponse<object> { Dados = null });
    }
}
