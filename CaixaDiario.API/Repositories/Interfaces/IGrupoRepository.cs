using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface IGrupoRepository
{
    Task<List<Grupo>> ListarTodosAsync();
    Task<List<Grupo>> ListarAtivosAsync();
    Task<Grupo?> ObterPorIdAsync(Guid id);
    Task<Grupo?> ObterPorNomeAsync(string nome);
    Task<Grupo> AdicionarAsync(Grupo grupo);
    Task<Grupo> AtualizarAsync(Grupo grupo);
    Task ReordenarAsync(List<(Guid Id, int Ordem)> novaOrdem);

    /// <summary>Quantidade de categorias (de qualquer status) que pertencem a este grupo.</summary>
    Task<int> ContarCategoriasAsync(Guid grupoId);
}
