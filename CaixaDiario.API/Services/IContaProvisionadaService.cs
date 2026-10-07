using CaixaDiario.API.DTOs.Registros;

namespace CaixaDiario.API.Services;

public interface IContaProvisionadaService
{
    Task<ContaProvisionadaDto> CriarAsync(CriarContaProvisionadaDto dto, Guid usuarioLogadoId, string perfil);
    Task<ContaProvisionadaDto> AtualizarAsync(Guid clienteId, Guid id, AtualizarContaProvisionadaDto dto, Guid usuarioLogadoId, string perfil);
    Task ExcluirAsync(Guid clienteId, Guid id, Guid usuarioLogadoId, string perfil);
}
