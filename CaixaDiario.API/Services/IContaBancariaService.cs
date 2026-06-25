using CaixaDiario.API.DTOs.ContasBancarias;

namespace CaixaDiario.API.Services;

public interface IContaBancariaService
{
    Task<List<ContaBancariaDto>> ListarPorClienteAsync(Guid clienteId, Guid usuarioLogadoId, string perfil);
    Task<ContaBancariaDto> ObterPorIdAsync(Guid id, Guid usuarioLogadoId, string perfil);
    Task<ContaBancariaDto> CriarAsync(CriarContaBancariaDto dto, Guid usuarioLogadoId, string perfil);
    Task<ContaBancariaDto> AtualizarAsync(Guid id, AtualizarContaBancariaDto dto, Guid usuarioLogadoId, string perfil);
    Task InativarAsync(Guid id, Guid usuarioLogadoId, string perfil);
}
