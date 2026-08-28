using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

/// <summary>
/// Histórico de importações — cada linha é um registro imutável de "esta transação (FitId ou
/// data+valor+descrição) já foi lançada nesta conta", usado só pra deduplicação em importações
/// futuras. A transação em si já foi lançada direto no RegistroDiario no momento da importação.
/// </summary>
public interface ITransacaoImportadaRepository
{
    Task<List<TransacaoImportada>> ListarPorContaAsync(Guid contaBancariaId);
    Task AdicionarLoteAsync(IEnumerable<TransacaoImportada> transacoes);
}
