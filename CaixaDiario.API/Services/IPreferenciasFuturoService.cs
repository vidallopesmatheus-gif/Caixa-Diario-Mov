using CaixaDiario.API.DTOs.Usuarios;

namespace CaixaDiario.API.Services;

public interface IPreferenciasFuturoService
{
    Task<PreferenciasFuturoDto> ObterAsync(Guid clienteId, Guid usuarioLogadoId, string perfil);
    Task<PreferenciasFuturoDto> AtualizarAsync(Guid clienteId, Guid usuarioLogadoId, string perfil, AtualizarPreferenciasFuturoDto dto);
}
