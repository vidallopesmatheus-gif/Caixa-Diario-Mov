using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface IOcorrenciaRecorrenteDispensadaRepository
{
    Task<List<OcorrenciaRecorrenteDispensada>> ListarPorClienteAsync(Guid clienteId);
    Task<bool> ExisteAsync(Guid recorrenciaId, DateOnly dataVencimento);
    Task AdicionarAsync(OcorrenciaRecorrenteDispensada ocorrencia);
}
