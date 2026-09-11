using CaixaDiario.API.DTOs.LinksConciliacao;

namespace CaixaDiario.API.Services;

public interface ILinkConciliacaoService
{
    Task<LinkConciliacaoDto> GerarAsync(Guid clienteId, Guid usuarioLogadoId, string perfil);
    Task<List<LinkConciliacaoDto>> ListarAsync(Guid clienteId, Guid usuarioLogadoId, string perfil);
    Task RevogarAsync(Guid id, Guid usuarioLogadoId, string perfil);
}
