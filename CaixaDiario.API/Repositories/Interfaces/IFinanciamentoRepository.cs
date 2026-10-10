using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface IFinanciamentoRepository
{
    Task<Financiamento?> ObterPorIdAsync(Guid id);
    Task<List<Financiamento>> ListarPorClienteAsync(Guid clienteId);
    Task<Financiamento> AdicionarAsync(Financiamento financiamento);
    Task<Financiamento> AtualizarAsync(Financiamento financiamento);
}
