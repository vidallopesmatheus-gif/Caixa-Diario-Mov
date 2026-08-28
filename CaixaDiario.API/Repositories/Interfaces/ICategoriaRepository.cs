using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface ICategoriaRepository
{
    Task<List<Categoria>> ListarAtivasAsync();
    Task<List<Categoria>> ListarTodasAsync();
    Task<Categoria?> ObterPorIdAsync(Guid id);
    Task<Categoria?> ObterPorNomeAsync(string nome);
    Task<Categoria> AdicionarAsync(Categoria categoria);
    Task<Categoria> AtualizarAsync(Categoria categoria);
    Task RemoverAsync(Categoria categoria);
    Task ReordenarAsync(List<(Guid Id, int Ordem)> novaOrdem);

    /// <summary>Conta lançamentos (entradas/saídas não excluídos) que usam a categoria pelo nome.</summary>
    Task<int> ContarUsoAsync(string nome);

    /// <summary>Reatribui todos os lançamentos que usam <paramref name="nomeOrigem"/> para a categoria de destino.</summary>
    Task MigrarUsoAsync(string nomeOrigem, string nomeDestino, string tipoDestino);
}
