using CaixaDiario.API.DTOs.PortalConciliacao;

namespace CaixaDiario.API.Services;

public interface IConciliacaoPublicaService
{
    Task<PortalConciliacaoDto> ObterPendentesAsync(string token);
    Task ClassificarAsync(string token, ClassificarPendentesDto dto);
}
