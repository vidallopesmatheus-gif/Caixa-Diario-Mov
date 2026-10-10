using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Repositories;

public class FinanciamentoRepository : IFinanciamentoRepository
{
    private readonly AppDbContext _context;

    public FinanciamentoRepository(AppDbContext context) => _context = context;

    public async Task<Financiamento?> ObterPorIdAsync(Guid id) =>
        await _context.Financiamentos.FirstOrDefaultAsync(f => f.Id == id);

    public async Task<List<Financiamento>> ListarPorClienteAsync(Guid clienteId) =>
        await _context.Financiamentos
            .Where(f => f.ClienteId == clienteId)
            .OrderByDescending(f => f.CriadoEm)
            .ToListAsync();

    public async Task<Financiamento> AdicionarAsync(Financiamento financiamento)
    {
        _context.Financiamentos.Add(financiamento);
        await _context.SaveChangesAsync();
        return financiamento;
    }

    public async Task<Financiamento> AtualizarAsync(Financiamento financiamento)
    {
        _context.Financiamentos.Update(financiamento);
        await _context.SaveChangesAsync();
        return financiamento;
    }

    public async Task RemoverAsync(Financiamento financiamento)
    {
        _context.Financiamentos.Remove(financiamento);
        await _context.SaveChangesAsync();
    }
}
