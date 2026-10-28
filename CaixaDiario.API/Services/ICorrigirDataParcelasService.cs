using CaixaDiario.API.DTOs.Admin;

namespace CaixaDiario.API.Services;

public interface ICorrigirDataParcelasService
{
    Task<CorrigirDataParcelasResultDto> CorrigirAsync(bool confirmar);
}
