using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface IRegistroRepository
{
    Task<RegistroDiario?> ObterPorContaEDataAsync(Guid contaBancariaId, DateOnly data);
    Task<RegistroDiario?> ObterPorClienteEDataAsync(Guid clienteId, DateOnly data);
    // Específico pra registros SEM conta vinculada (ContaBancariaId null) — diferente de
    // ObterPorClienteEDataAsync, que pega o primeiro registro do cliente naquela data seja qual
    // for a conta, o que é ambíguo quando o cliente tem mais de uma conta com registro no mesmo dia.
    Task<RegistroDiario?> ObterPorClienteEDataSemContaAsync(Guid clienteId, DateOnly data);
    Task<List<RegistroDiario>> ListarPorClienteAsync(Guid clienteId);
    Task<List<RegistroDiario>> ListarPorContaAsync(Guid contaBancariaId);
    Task<RegistroDiario> AdicionarAsync(RegistroDiario registro);
    Task<RegistroDiario> AtualizarAsync(RegistroDiario registro);
    Task<List<RegistroDiario>> ListarPorPeriodoAsync(Guid clienteId, DateOnly de, DateOnly ate);
    // Inclui registros com Excluido=true — usado antes de apagar uma conta bancária de verdade,
    // pra não deixar um dia soft-deletado com FK pra uma conta que não existe mais.
    Task<int> ContarTodosPorContaAsync(Guid contaBancariaId);
}
