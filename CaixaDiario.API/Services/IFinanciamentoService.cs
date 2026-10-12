using CaixaDiario.API.DTOs.Financiamentos;

namespace CaixaDiario.API.Services;

public interface IFinanciamentoService
{
    Task<FinanciamentoDto> CriarAsync(Guid clienteId, Guid usuarioLogadoId, string perfil, CriarFinanciamentoDto dto);
    Task<List<FinanciamentoDto>> ListarAsync(Guid clienteId, Guid usuarioLogadoId, string perfil);
    Task<List<ParcelaFinanciamentoDto>> ListarParcelasAsync(Guid financiamentoId, Guid usuarioLogadoId, string perfil);
    Task<ParcelaFinanciamentoDto> BaixarParcelaAsync(Guid financiamentoId, Guid usuarioLogadoId, string perfil, BaixarParcelaFinanciamentoDto dto);
    Task EstornarParcelaAsync(Guid financiamentoId, Guid parcelaId, Guid usuarioLogadoId, string perfil);
    Task<AdiantamentoResultDto> AdiantarAsync(Guid financiamentoId, Guid usuarioLogadoId, string perfil, AdiantarFinanciamentoDto dto);
    Task ExcluirAsync(Guid financiamentoId, Guid usuarioLogadoId, string perfil, bool removerAdiantamentos = true);
}
