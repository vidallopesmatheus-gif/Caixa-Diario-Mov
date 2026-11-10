using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _context;

    public RefreshTokenRepository(AppDbContext context) => _context = context;

    public async Task<RefreshToken?> ObterPorHashAsync(string tokenHash) =>
        await _context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

    public async Task AdicionarAsync(RefreshToken token)
    {
        _context.RefreshTokens.Add(token);
        await _context.SaveChangesAsync();
    }

    public async Task AtualizarAsync(RefreshToken token)
    {
        _context.RefreshTokens.Update(token);
        await _context.SaveChangesAsync();
    }

    public async Task RevogarFamiliaAsync(Guid familiaId, DateTime agora)
    {
        var ativos = await _context.RefreshTokens.Where(t => t.FamiliaId == familiaId && t.RevogadoEm == null).ToListAsync();
        foreach (var t in ativos) t.RevogadoEm = agora;
        await _context.SaveChangesAsync();
    }

    public async Task RemoverExpiradosDoUsuarioAsync(Guid usuarioId, DateTime agora)
    {
        var vencidos = await _context.RefreshTokens.Where(t => t.UsuarioId == usuarioId && t.ExpiraEm < agora).ToListAsync();
        if (vencidos.Count == 0) return;
        _context.RefreshTokens.RemoveRange(vencidos);
        await _context.SaveChangesAsync();
    }
}
