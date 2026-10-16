using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface ICategoriaRepository
{
    // Correção de integridade (Parte 1): categoria agora é por cliente — estes dois métodos
    // globais só continuam existindo pro admin cascatear Tipo quando muda o Bloco de um Grupo
    // (GrupoService.AtualizarAsync), que tem que afetar a cópia de TODOS os clientes de uma vez.
    // Qualquer outro uso (classificação de lançamento, listagem pra tela do cliente) tem que usar
    // a variante "PorClienteAsync" — nunca estas duas.
    Task<List<Categoria>> ListarAtivasAsync();
    Task<List<Categoria>> ListarTodasAsync();

    Task<List<Categoria>> ListarAtivasPorClienteAsync(Guid clienteId);
    Task<List<Categoria>> ListarTodasPorClienteAsync(Guid clienteId);

    /// <summary>O "molde" padrão (ClienteId == null, Origem == "Padrao") ativo — copiado pra cada cliente novo.</summary>
    Task<List<Categoria>> ListarPadraoAtivasAsync();

    Task<Categoria?> ObterPorIdAsync(Guid id);
    Task<Categoria?> ObterPorNomeAsync(Guid clienteId, string nome);
    Task<Categoria> AdicionarAsync(Categoria categoria);
    Task AdicionarVariasAsync(List<Categoria> categorias);
    Task<Categoria> AtualizarAsync(Categoria categoria);
    Task RemoverAsync(Categoria categoria);
    Task ReordenarAsync(List<(Guid Id, int Ordem)> novaOrdem);

    /// <summary>Conta lançamentos (entradas/saídas não excluídos) do cliente que usam a categoria pelo nome.</summary>
    Task<int> ContarUsoAsync(Guid clienteId, string nome);

    /// <summary>Quantidade de lançamentos (entradas/saídas não excluídos) do cliente, por nome de categoria — uma consulta só, pra relatórios que olham todas as categorias de uma vez (ver ReclassificacaoCategoriasService).</summary>
    Task<Dictionary<string, (int Entradas, int Saidas)>> ContarUsoPorCategoriaAsync(Guid clienteId);

    /// <summary>Reatribui, só dentro do cliente, todos os lançamentos que usam <paramref name="nomeOrigem"/> para a categoria de destino.</summary>
    Task MigrarUsoAsync(Guid clienteId, string nomeOrigem, string nomeDestino, string tipoDestino);
}
