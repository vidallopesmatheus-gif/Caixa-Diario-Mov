using CaixaDiario.API.DTOs.Conciliacao;

namespace CaixaDiario.API.Services;

public interface IConciliacaoService
{
    Task<List<SugestaoVinculoDto>> ListarSugestoesAsync(
        Guid clienteId, DateOnly de, DateOnly ate, Guid usuarioLogadoId, string perfil, Guid? contaBancariaId = null);
}
