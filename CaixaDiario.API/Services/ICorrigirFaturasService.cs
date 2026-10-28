using CaixaDiario.API.DTOs.Admin;

namespace CaixaDiario.API.Services;

public interface ICorrigirFaturasService
{
    Task<CorrigirFaturasResultDto> CorrigirAsync(bool confirmar);
}
