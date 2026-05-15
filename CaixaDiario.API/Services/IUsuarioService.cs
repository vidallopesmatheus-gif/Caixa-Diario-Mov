// CaixaDiario.API/Services/IUsuarioService.cs
using CaixaDiario.API.DTOs.Usuarios;

namespace CaixaDiario.API.Services;

public interface IUsuarioService
{
    Task<List<UsuarioDto>> ListarAsync();
    Task<UsuarioDto> ObterPorIdAsync(Guid id);
    Task<UsuarioDto> CriarAsync(CriarUsuarioDto dto, string nomeUsuarioLogado);
    Task<UsuarioDto> AtualizarAsync(Guid id, AtualizarUsuarioDto dto, string nomeUsuarioLogado);
    Task DesativarAsync(Guid id, string nomeUsuarioLogado);
}
