using CaixaDiario.API.DTOs.Relatorios;

namespace CaixaDiario.API.Services;

public interface IPrevistoRealizadoService
{
    Task<PrevistoRealizadoDto> ObterAsync(
        Guid clienteId, int meses, Guid usuarioLogadoId, string perfil, bool incluirAvulsosVinculados = false);

    // Item 7, ponto 5: "tocar num item mostra o histórico dele" — previsto × realizado dos últimos
    // meses, só desse favorecido (mesma chave retornada em ItemPrevistoRealizadoDto.Chave).
    Task<HistoricoItemPrevistoRealizadoDto> ObterHistoricoItemAsync(
        Guid clienteId, string chave, int meses, Guid usuarioLogadoId, string perfil, bool incluirAvulsosVinculados = false);
}
