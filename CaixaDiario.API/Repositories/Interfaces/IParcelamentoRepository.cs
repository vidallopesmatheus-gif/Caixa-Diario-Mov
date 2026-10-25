using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface IParcelamentoRepository
{
    Task<Parcelamento?> ObterPorIdAsync(Guid id);
    Task<List<Parcelamento>> ListarPorClienteAsync(Guid clienteId);
    Task<List<Parcelamento>> ListarAtivosPorContaAsync(Guid contaCartaoId);
    Task<Parcelamento> AdicionarAsync(Parcelamento parcelamento);
    Task<Parcelamento> AtualizarAsync(Parcelamento parcelamento);
}
