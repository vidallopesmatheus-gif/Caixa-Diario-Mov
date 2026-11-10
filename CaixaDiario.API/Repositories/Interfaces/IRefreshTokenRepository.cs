using CaixaDiario.API.Models;

namespace CaixaDiario.API.Repositories.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> ObterPorHashAsync(string tokenHash);
    Task AdicionarAsync(RefreshToken token);
    Task AtualizarAsync(RefreshToken token);
    Task RevogarFamiliaAsync(Guid familiaId, DateTime agora);
    // Limpeza oportunista no login — a tabela nunca cresce com tokens vencidos de um usuário.
    Task RemoverExpiradosDoUsuarioAsync(Guid usuarioId, DateTime agora);
}
