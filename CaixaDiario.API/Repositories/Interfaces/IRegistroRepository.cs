using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface IRegistroRepository
{
    Task<RegistroDiario?> ObterPorContaEDataAsync(Guid contaBancariaId, DateOnly data);
    Task<RegistroDiario?> ObterPorClienteEDataAsync(Guid clienteId, DateOnly data);
    Task<List<RegistroDiario>> ListarPorClienteAsync(Guid clienteId);
    Task<List<RegistroDiario>> ListarPorContaAsync(Guid contaBancariaId);
    Task<RegistroDiario> AdicionarAsync(RegistroDiario registro);
    Task<RegistroDiario> AtualizarAsync(RegistroDiario registro);
    Task<List<RegistroDiario>> ListarPorPeriodoAsync(Guid clienteId, DateOnly de, DateOnly ate);
    // Inclui registros com Excluido=true — usado antes de apagar uma conta bancária de verdade,
    // pra não deixar um dia soft-deletado com FK pra uma conta que não existe mais.
    Task<int> ContarTodosPorContaAsync(Guid contaBancariaId);
}
