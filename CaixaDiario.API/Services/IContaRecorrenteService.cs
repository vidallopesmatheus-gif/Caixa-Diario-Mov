using CaixaDiario.API.DTOs.ContasRecorrentes;

namespace CaixaDiario.API.Services;

public interface IContaRecorrenteService
{
    Task<List<ContaRecorrenteDto>> ListarPorClienteAsync(Guid clienteId, Guid usuarioLogadoId, string perfil);
    Task<ContaRecorrenteDto> CriarAsync(CriarContaRecorrenteDto dto, Guid usuarioLogadoId, string perfil);
    Task<ContaRecorrenteDto> AtualizarAsync(Guid clienteId, Guid id, AtualizarContaRecorrenteDto dto, Guid usuarioLogadoId, string perfil);
    Task DesativarAsync(Guid clienteId, Guid id, Guid usuarioLogadoId, string perfil);
}
