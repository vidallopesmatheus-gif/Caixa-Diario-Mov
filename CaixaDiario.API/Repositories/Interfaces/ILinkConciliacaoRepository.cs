using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface ILinkConciliacaoRepository
{
    Task<LinkConciliacao?> ObterPorIdAsync(Guid id);
    Task<LinkConciliacao?> ObterPorTokenAsync(string token);
    Task<LinkConciliacao?> ObterAtivoPorClienteAsync(Guid clienteId);
    Task<List<LinkConciliacao>> ListarPorClienteAsync(Guid clienteId);
    Task<LinkConciliacao> AdicionarAsync(LinkConciliacao link);
    Task<LinkConciliacao> AtualizarAsync(LinkConciliacao link);
}
