using CaixaDiario.API.DTOs.Regras;

namespace CaixaDiario.API.Services;

public interface IRegraCategorizacaoService
{
    Task<List<RegraCategorizacaoDto>> ListarAsync(Guid clienteId, Guid usuarioLogadoId, string perfil);
    Task<RegraCategorizacaoDto> CriarAsync(Guid clienteId, CriarRegraDto dto, Guid usuarioLogadoId, string perfil);
    Task<RegraCategorizacaoDto> AtualizarAsync(Guid id, AtualizarRegraDto dto, Guid usuarioLogadoId, string perfil);
    Task DesativarAsync(Guid id, Guid usuarioLogadoId, string perfil);
    Task ReativarAsync(Guid id, Guid usuarioLogadoId, string perfil);
    Task ExcluirAsync(Guid id, Guid usuarioLogadoId, string perfil);
    Task ReordenarAsync(Guid clienteId, List<Guid> ids, Guid usuarioLogadoId, string perfil);
    Task<int> ContarCorrespondenciasAsync(Guid contaBancariaId, string tipo, string descricaoReferencia, Guid usuarioLogadoId, string perfil);
    Task<AplicarRetroativoResultDto> AplicarRetroativamenteAsync(Guid id, Guid usuarioLogadoId, string perfil);
}
