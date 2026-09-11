using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Repositories;

public class LinkConciliacaoRepository : ILinkConciliacaoRepository
{
    private readonly AppDbContext _context;

    public LinkConciliacaoRepository(AppDbContext context) => _context = context;

    public async Task<LinkConciliacao?> ObterPorIdAsync(Guid id) =>
        await _context.LinksConciliacao.FindAsync(id);

    public async Task<LinkConciliacao?> ObterPorTokenAsync(string token) =>
        await _context.LinksConciliacao.FirstOrDefaultAsync(l => l.Token == token);

    public async Task<LinkConciliacao?> ObterAtivoPorClienteAsync(Guid clienteId) =>
        await _context.LinksConciliacao
            .Where(l => l.ClienteId == clienteId && l.RevogadoEm == null && l.ExpiraEm > DateTime.UtcNow)
            .OrderByDescending(l => l.CriadoEm)
            .FirstOrDefaultAsync();

    public async Task<List<LinkConciliacao>> ListarPorClienteAsync(Guid clienteId) =>
        await _context.LinksConciliacao
            .Where(l => l.ClienteId == clienteId)
            .OrderByDescending(l => l.CriadoEm)
            .ToListAsync();

    public async Task<LinkConciliacao> AdicionarAsync(LinkConciliacao link)
    {
        _context.LinksConciliacao.Add(link);
        await _context.SaveChangesAsync();
        return link;
    }

    public async Task<LinkConciliacao> AtualizarAsync(LinkConciliacao link)
    {
        _context.LinksConciliacao.Update(link);
        await _context.SaveChangesAsync();
        return link;
    }
}
