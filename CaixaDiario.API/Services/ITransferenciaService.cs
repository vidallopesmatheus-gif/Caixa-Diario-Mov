using CaixaDiario.API.DTOs.Transferencias;

namespace CaixaDiario.API.Services;

public interface ITransferenciaService
{
    Task<TransferenciaDto> CriarAsync(CriarTransferenciaDto dto, Guid usuarioLogadoId, string perfil);
    Task<List<TransferenciaDto>> ListarPorClienteAsync(Guid clienteId, Guid usuarioLogadoId, string perfil);
    Task EstornarAsync(Guid id, Guid usuarioLogadoId, string perfil);
    Task<TransferenciaDto> ConverterLancamentoAsync(ConverterLancamentoEmTransferenciaDto dto, Guid usuarioLogadoId, string perfil);
}
