using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Repositories;

public class PagamentoFaturaRepository : IPagamentoFaturaRepository
{
    private readonly AppDbContext _context;

    public PagamentoFaturaRepository(AppDbContext context) => _context = context;

    public async Task<PagamentoFatura?> ObterPorIdAsync(Guid id) =>
        await _context.PagamentosFatura.FindAsync(id);

    public async Task<List<PagamentoFatura>> ListarPorContaCartaoAsync(Guid contaCartaoId) =>
        await _context.PagamentosFatura
            .Where(p => p.ContaCartaoId == contaCartaoId)
            .OrderByDescending(p => p.Data)
            .ToListAsync();

    public async Task<PagamentoFatura> AdicionarAsync(PagamentoFatura pagamento)
    {
        _context.PagamentosFatura.Add(pagamento);
        await _context.SaveChangesAsync();
        return pagamento;
    }

    public async Task RemoverAsync(PagamentoFatura pagamento)
    {
        _context.PagamentosFatura.Remove(pagamento);
        await _context.SaveChangesAsync();
    }
}
