using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Repositories;

public class TransacaoImportadaRepository : ITransacaoImportadaRepository
{
    private readonly AppDbContext _context;

    public TransacaoImportadaRepository(AppDbContext context) => _context = context;

    public async Task<List<TransacaoImportada>> ListarPendentesPorContaAsync(Guid contaBancariaId) =>
        await _context.TransacoesImportadas
            .Where(t => t.ContaBancariaId == contaBancariaId && t.Status == "Pendente")
            .OrderBy(t => t.Data)
            .ToListAsync();

    public async Task<bool> ExisteFitIdAsync(Guid contaBancariaId, string fitId) =>
        await _context.TransacoesImportadas
            .AnyAsync(t => t.ContaBancariaId == contaBancariaId && t.FitId == fitId);

    public async Task AdicionarLoteAsync(IEnumerable<TransacaoImportada> transacoes)
    {
        _context.TransacoesImportadas.AddRange(transacoes);
        await _context.SaveChangesAsync();
    }

    public async Task<TransacaoImportada?> ObterPorIdAsync(Guid id) =>
        await _context.TransacoesImportadas.FindAsync(id);

    public async Task AtualizarLoteAsync(IEnumerable<TransacaoImportada> transacoes)
    {
        _context.TransacoesImportadas.UpdateRange(transacoes);
        await _context.SaveChangesAsync();
    }

    public async Task<List<TransacaoImportada>> ObterPorIdsAsync(IEnumerable<Guid> ids) =>
        await _context.TransacoesImportadas
            .Where(t => ids.Contains(t.Id))
            .ToListAsync();
}
