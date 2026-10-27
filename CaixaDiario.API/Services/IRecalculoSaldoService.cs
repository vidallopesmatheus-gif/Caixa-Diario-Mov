using CaixaDiario.API.DTOs.Admin;

namespace CaixaDiario.API.Services;

public interface IRecalculoSaldoService
{
    Task<RecalculoSaldoResultDto> RecalcularTodasAsContasAsync(bool confirmar);
}
