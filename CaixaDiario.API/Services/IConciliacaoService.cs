using CaixaDiario.API.DTOs.Conciliacao;

namespace CaixaDiario.API.Services;

public interface IConciliacaoService
{
    Task<List<SugestaoVinculoDto>> ListarSugestoesAsync(
        Guid clienteId, DateOnly de, DateOnly ate, Guid usuarioLogadoId, string perfil, Guid? contaBancariaId = null);

    // Item 3.3: persiste a decisão de "Ignorar" uma sugestão — ela não volta a aparecer.
    Task IgnorarAsync(Guid clienteId, Guid contaProvisionadaId, Guid lancamentoId, Guid usuarioLogadoId, string perfil);
}
