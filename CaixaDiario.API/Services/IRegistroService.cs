// CaixaDiario.API/Services/IRegistroService.cs
using CaixaDiario.API.DTOs.Registros;

namespace CaixaDiario.API.Services;

public interface IRegistroService
{
    Task<List<RegistroDto>> ListarPorClienteAsync(Guid clienteId, Guid usuarioLogadoId, string perfil);
    Task<RegistroDto> ObterPorDataAsync(Guid clienteId, DateOnly data, Guid usuarioLogadoId, string perfil);
    Task<(RegistroDto dto, bool criado)> SalvarAsync(CriarRegistroDto dto, string nomeUsuario);
    Task ExcluirAsync(Guid clienteId, DateOnly data, string motivo, Guid usuarioLogadoId, string perfil);
}
