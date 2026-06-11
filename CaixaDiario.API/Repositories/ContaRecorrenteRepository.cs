using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Repositories;

public class ContaRecorrenteRepository : IContaRecorrenteRepository
{
    private readonly AppDbContext _context;

    public ContaRecorrenteRepository(AppDbContext context) => _context = context;

    public async Task<List<ContaRecorrente>> GetByUsuarioIdAsync(Guid usuarioId) =>
        await _context.ContasRecorrentes
            .Where(c => c.UsuarioId == usuarioId)
            .OrderBy(c => c.DiaVencimento)
            .ToListAsync();

    public async Task<ContaRecorrente?> GetByIdAsync(Guid id) =>
        await _context.ContasRecorrentes.FindAsync(id);

    public async Task<ContaRecorrente> CreateAsync(ContaRecorrente conta)
    {
        _context.ContasRecorrentes.Add(conta);
        await _context.SaveChangesAsync();
        return conta;
    }

    public async Task<ContaRecorrente> UpdateAsync(ContaRecorrente conta)
    {
        _context.ContasRecorrentes.Update(conta);
        await _context.SaveChangesAsync();
        return conta;
    }

    public async Task DeleteAsync(Guid id)
    {
        var conta = await _context.ContasRecorrentes.FindAsync(id);
        if (conta is not null)
        {
            _context.ContasRecorrentes.Remove(conta);
            await _context.SaveChangesAsync();
        }
    }
}
