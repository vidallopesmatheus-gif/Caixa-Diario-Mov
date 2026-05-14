using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface IRegistroRepository
{
    Task<RegistroDiario?> ObterPorClienteEDataAsync(Guid clienteId, DateOnly data);
    Task<List<RegistroDiario>> ListarPorClienteAsync(Guid clienteId);
    Task<RegistroDiario> AdicionarAsync(RegistroDiario registro);
    Task<RegistroDiario> AtualizarAsync(RegistroDiario registro);
}
