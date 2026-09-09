using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Repositories;

public class RegraCategorizacaoRepository : IRegraCategorizacaoRepository
{
    private readonly AppDbContext _context;

    public RegraCategorizacaoRepository(AppDbContext context) => _context = context;

    public async Task<RegraCategorizacao?> ObterPorIdAsync(Guid id) =>
        await _context.RegrasCategorizacao.Include(r => r.ContaBancaria).Include(r => r.ContaContrapartida)
            .FirstOrDefaultAsync(r => r.Id == id);

    public async Task<List<RegraCategorizacao>> ListarPorClienteAsync(Guid clienteId) =>
        await _context.RegrasCategorizacao
            .Include(r => r.ContaBancaria)
            .Include(r => r.ContaContrapartida)
            .Where(r => r.ClienteId == clienteId)
            .OrderBy(r => r.Ordem)
            .ThenBy(r => r.CriadoEm)
            .ToListAsync();

    public async Task<List<RegraCategorizacao>> ListarAtivasPorContaAsync(Guid contaBancariaId) =>
        await _context.RegrasCategorizacao
            .Where(r => r.ContaBancariaId == contaBancariaId && r.Ativa)
            .OrderBy(r => r.Ordem)
            .ThenBy(r => r.CriadoEm)
            .ToListAsync();

    public async Task<RegraCategorizacao> AdicionarAsync(RegraCategorizacao regra)
    {
        _context.RegrasCategorizacao.Add(regra);
        await _context.SaveChangesAsync();
        return regra;
    }

    public async Task<RegraCategorizacao> AtualizarAsync(RegraCategorizacao regra)
    {
        _context.RegrasCategorizacao.Update(regra);
        await _context.SaveChangesAsync();
        return regra;
    }

    public async Task RemoverAsync(RegraCategorizacao regra)
    {
        _context.RegrasCategorizacao.Remove(regra);
        await _context.SaveChangesAsync();
    }
}
