using CaixaDiario.API.DTOs.Admin;

namespace CaixaDiario.API.Services;

public interface ILimpezaTransferenciasService
{
    Task<LimpezaContrapartidasOrfasResultDto> LimparContrapartidasOrfasAsync(bool confirmar);
}
