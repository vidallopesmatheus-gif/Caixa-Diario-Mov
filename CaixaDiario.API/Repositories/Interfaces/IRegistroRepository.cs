using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface IRegistroRepository
{
    Task<RegistroDiario?> ObterPorContaEDataAsync(Guid contaBancariaId, DateOnly data);
    Task<List<RegistroDiario>> ListarPorClienteAsync(Guid clienteId);
    Task<List<RegistroDiario>> ListarPorContaAsync(Guid contaBancariaId);
    Task<RegistroDiario> AdicionarAsync(RegistroDiario registro);
    Task<RegistroDiario> AtualizarAsync(RegistroDiario registro);
    Task<List<RegistroDiario>> ListarPorPeriodoAsync(Guid clienteId, DateOnly de, DateOnly ate);
}
