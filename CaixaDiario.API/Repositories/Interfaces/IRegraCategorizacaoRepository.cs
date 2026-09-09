using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface IRegraCategorizacaoRepository
{
    Task<RegraCategorizacao?> ObterPorIdAsync(Guid id);
    Task<List<RegraCategorizacao>> ListarPorClienteAsync(Guid clienteId);
    Task<List<RegraCategorizacao>> ListarAtivasPorContaAsync(Guid contaBancariaId);
    Task<RegraCategorizacao> AdicionarAsync(RegraCategorizacao regra);
    Task<RegraCategorizacao> AtualizarAsync(RegraCategorizacao regra);
    Task RemoverAsync(RegraCategorizacao regra);
}
