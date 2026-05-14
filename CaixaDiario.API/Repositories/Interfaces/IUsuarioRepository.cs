using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface IUsuarioRepository
{
    Task<Usuario?> ObterPorIdAsync(Guid id);
    Task<Usuario?> ObterPorNomeUsuarioAsync(string nomeUsuario);
    Task<List<Usuario>> ListarAtivosAsync();
    Task<bool> ExisteNomeUsuarioAsync(string nomeUsuario, Guid? excluirId = null);
    Task<Usuario> AdicionarAsync(Usuario usuario);
    Task<Usuario> AtualizarAsync(Usuario usuario);
}
