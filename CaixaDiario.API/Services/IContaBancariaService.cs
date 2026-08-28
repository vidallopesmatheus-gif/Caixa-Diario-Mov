using CaixaDiario.API.DTOs.ContasBancarias;

namespace CaixaDiario.API.Services;

public interface IContaBancariaService
{
    Task<List<ContaBancariaDto>> ListarPorClienteAsync(Guid clienteId, Guid usuarioLogadoId, string perfil);
    Task<ContaBancariaDto> ObterPorIdAsync(Guid id, Guid usuarioLogadoId, string perfil);
    Task<ContaBancariaDto> CriarAsync(CriarContaBancariaDto dto, Guid usuarioLogadoId, string perfil);
    Task<ContaBancariaDto> AtualizarAsync(Guid id, AtualizarContaBancariaDto dto, Guid usuarioLogadoId, string perfil);
    Task InativarAsync(Guid id, Guid usuarioLogadoId, string perfil);
    Task<List<LancamentoExtratoDto>> ObterExtratoAsync(Guid contaId, Guid usuarioLogadoId, string perfil, DateOnly? de, DateOnly? ate);
    Task<PendenciasContaDto> ObterPendenciasAsync(Guid contaId, Guid usuarioLogadoId, string perfil);
    Task<ContaBancariaDto> RegistrarRendimentoAsync(Guid contaId, RegistrarRendimentoDto dto, Guid usuarioLogadoId, string perfil);
    Task<ContaBancariaDto> VincularMetaAsync(Guid contaId, Guid metaId, Guid usuarioLogadoId, string perfil);
    Task<ContaBancariaDto> DesvincularMetaAsync(Guid contaId, Guid metaId, Guid usuarioLogadoId, string perfil);
}
