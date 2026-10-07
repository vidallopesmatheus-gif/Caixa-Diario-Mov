using CaixaDiario.API.DTOs.Admin;

namespace CaixaDiario.API.Services;

public interface IMigracaoRecorrenciasService
{
    Task<MigracaoRecorrenciasResultDto> MigrarOcorrenciasSemContaCorretaAsync(bool confirmar);
}
