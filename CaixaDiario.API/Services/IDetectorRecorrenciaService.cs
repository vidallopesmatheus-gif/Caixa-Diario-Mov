using CaixaDiario.API.DTOs.ContasRecorrentes;

namespace CaixaDiario.API.Services;

public interface IDetectorRecorrenciaService
{
    Task<List<SugestaoRecorrenciaDto>> ListarSugestoesAsync(Guid clienteId, Guid usuarioLogadoId, string perfil);
    Task IgnorarAsync(Guid clienteId, string chave, Guid usuarioLogadoId, string perfil);
}
