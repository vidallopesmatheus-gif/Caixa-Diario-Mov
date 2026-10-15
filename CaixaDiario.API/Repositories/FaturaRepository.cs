using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Repositories;

public class FaturaRepository : IFaturaRepository
{
    private readonly AppDbContext _context;

    public FaturaRepository(AppDbContext context) => _context = context;

    public async Task<Fatura?> ObterPorIdAsync(Guid id) =>
        await _context.Faturas.FindAsync(id);

    public async Task<List<Fatura>> ListarPorContaAsync(Guid contaBancariaId) =>
        await _context.Faturas
            .Where(f => f.ContaBancariaId == contaBancariaId)
            .OrderByDescending(f => f.DataVencimento)
            .ToListAsync();

    public async Task<List<string>> ListarBancosDistintosPorClienteAsync(Guid clienteId) =>
        await _context.Faturas
            .Where(f => f.ClienteId == clienteId)
            .Select(f => f.Banco)
            .Distinct()
            .OrderBy(b => b)
            .ToListAsync();

    public async Task<Fatura> AdicionarAsync(Fatura fatura)
    {
        _context.Faturas.Add(fatura);
        await _context.SaveChangesAsync();
        return fatura;
    }

    public async Task<Fatura> AtualizarAsync(Fatura fatura)
    {
        _context.Faturas.Update(fatura);
        await _context.SaveChangesAsync();
        return fatura;
    }
}
