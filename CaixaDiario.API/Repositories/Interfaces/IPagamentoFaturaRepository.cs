using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface IPagamentoFaturaRepository
{
    Task<PagamentoFatura?> ObterPorIdAsync(Guid id);
    Task<List<PagamentoFatura>> ListarPorContaCartaoAsync(Guid contaCartaoId);
    Task<PagamentoFatura> AdicionarAsync(PagamentoFatura pagamento);
    Task RemoverAsync(PagamentoFatura pagamento);
}
