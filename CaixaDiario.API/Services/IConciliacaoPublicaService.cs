using CaixaDiario.API.DTOs.PortalConciliacao;

namespace CaixaDiario.API.Services;

public interface IConciliacaoPublicaService
{
    Task<PortalConciliacaoDto> ObterPendentesAsync(string token);
    Task ClassificarAsync(string token, ClassificarPendentesDto dto);
    // Cliente pede pra "lembrar" uma classificação em lote pra próxima vez — nasce como sugestão
    // (Sugerida=true, Ativa=false), só vale depois que o consultor aprovar internamente.
    Task SugerirRegraAsync(string token, SugerirRegraPortalDto dto);
}
