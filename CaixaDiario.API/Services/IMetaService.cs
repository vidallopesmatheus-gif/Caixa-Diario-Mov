using CaixaDiario.API.DTOs.Metas;

namespace CaixaDiario.API.Services;

public interface IMetaService
{
    Task<MetaAnualDto> ObterMetaAsync(Guid clienteId, int ano, Guid usuarioLogadoId, string perfil);
    Task<List<MetaAnualDto>> ListarMetasAsync(Guid clienteId, Guid usuarioLogadoId, string perfil);
    Task<MetaAnualDto> SalvarMetaAsync(SalvarMetaAnualDto dto, Guid usuarioLogadoId, string perfil);
    Task ExcluirMetaAsync(Guid id, Guid usuarioLogadoId, string perfil);
    Task<MetaAnualDto> ConquistarAsync(Guid metaId, Guid usuarioLogadoId, string perfil, ConquistarMetaDto dto);
    Task<DesfazerConquistaResultDto> DesfazerConquistaAsync(Guid metaId, Guid usuarioLogadoId, string perfil);
}
