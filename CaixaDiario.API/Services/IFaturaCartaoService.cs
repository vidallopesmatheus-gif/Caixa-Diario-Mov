using CaixaDiario.API.DTOs.FaturasCartao;

namespace CaixaDiario.API.Services;

public interface IFaturaCartaoService
{
    Task<List<FaturaCartaoDto>> ListarFaturasAsync(Guid contaCartaoId, Guid usuarioLogadoId, string perfil);
    Task<FaturaCartaoDto?> SugerirFaturaAsync(Guid contaCartaoId, decimal valor, DateOnly data, Guid usuarioLogadoId, string perfil);
    Task<PagamentoFaturaDto> VincularPagamentoAsync(VincularPagamentoFaturaDto dto, Guid usuarioLogadoId, string perfil);
    Task DesvincularPagamentoAsync(Guid id, Guid usuarioLogadoId, string perfil);
}
