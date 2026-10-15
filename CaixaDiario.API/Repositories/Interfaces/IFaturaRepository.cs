using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface IFaturaRepository
{
    Task<Fatura?> ObterPorIdAsync(Guid id);
    Task<List<Fatura>> ListarPorContaAsync(Guid contaBancariaId);
    // Pro combobox editável de banco/cartão (item 1 do pedido): valores já usados por este cliente.
    Task<List<string>> ListarBancosDistintosPorClienteAsync(Guid clienteId);
    Task<Fatura> AdicionarAsync(Fatura fatura);
    Task<Fatura> AtualizarAsync(Fatura fatura);
}
