using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface IContaRecorrenteRepository
{
    Task<List<ContaRecorrente>> ListarAtivasPorClienteAsync(Guid clienteId);
    // Todas, de todos os clientes, ativas ou não — usado pela migration admin (Fase 0.2) pra achar
    // toda ocorrência já materializada que pode estar presa no registro de uma conta errada.
    Task<List<ContaRecorrente>> ListarTodasAsync();
    // Inclui inativas — a FK pra conta_bancaria é obrigatória mesmo numa recorrência desativada,
    // então ela também bloqueia a exclusão física da conta (ver ContaBancariaService.ExcluirAsync).
    Task<int> ContarPorContaBancariaAsync(Guid contaBancariaId);
    Task<ContaRecorrente?> ObterPorIdAsync(Guid clienteId, Guid id);
    Task<ContaRecorrente> AdicionarAsync(ContaRecorrente conta);
    Task<ContaRecorrente> AtualizarAsync(ContaRecorrente conta);
}
