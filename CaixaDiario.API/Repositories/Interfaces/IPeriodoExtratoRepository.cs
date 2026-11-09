using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface IPeriodoExtratoRepository
{
    Task<List<PeriodoExtrato>> ListarPorContaAsync(Guid contaBancariaId);
    Task AdicionarAsync(PeriodoExtrato periodo);
}
