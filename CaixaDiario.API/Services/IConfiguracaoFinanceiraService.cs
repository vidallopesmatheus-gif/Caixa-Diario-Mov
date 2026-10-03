using CaixaDiario.API.DTOs.ConfiguracaoFinanceira;

namespace CaixaDiario.API.Services;

public interface IConfiguracaoFinanceiraService
{
    Task<ConfiguracaoFinanceiraDto> ObterAsync(Guid clienteId, Guid usuarioLogadoId, string perfil);
    Task<ConfiguracaoFinanceiraDto> AtualizarAsync(
        Guid clienteId, AtualizarConfiguracaoFinanceiraDto dto, Guid usuarioLogadoId, string perfil);
}
