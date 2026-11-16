using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface IConferenciaSaldoRepository
{
    Task AdicionarAsync(ConferenciaSaldo conferencia);
    // Mais recente primeiro.
    Task<List<ConferenciaSaldo>> ListarPorContaAsync(Guid contaBancariaId);
}
