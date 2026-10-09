using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface IDuplicataDispensadaRepository
{
    Task<List<DuplicataDispensada>> ListarPorContaAsync(Guid contaBancariaId);
    Task AdicionarAsync(DuplicataDispensada dispensada);
}
