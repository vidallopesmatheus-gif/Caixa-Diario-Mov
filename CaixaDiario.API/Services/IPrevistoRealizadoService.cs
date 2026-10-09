using CaixaDiario.API.DTOs.Relatorios;

namespace CaixaDiario.API.Services;

public interface IPrevistoRealizadoService
{
    Task<PrevistoRealizadoDto> ObterAsync(
        Guid clienteId, int meses, Guid usuarioLogadoId, string perfil, bool incluirAvulsosVinculados = false);
}
