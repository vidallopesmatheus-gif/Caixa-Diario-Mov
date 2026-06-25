using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface ITransacaoImportadaRepository
{
    Task<List<TransacaoImportada>> ListarPendentesPorContaAsync(Guid contaBancariaId);
    Task<bool> ExisteFitIdAsync(Guid contaBancariaId, string fitId);
    Task AdicionarLoteAsync(IEnumerable<TransacaoImportada> transacoes);
    Task<TransacaoImportada?> ObterPorIdAsync(Guid id);
    Task AtualizarLoteAsync(IEnumerable<TransacaoImportada> transacoes);
    Task<List<TransacaoImportada>> ObterPorIdsAsync(IEnumerable<Guid> ids);
}
