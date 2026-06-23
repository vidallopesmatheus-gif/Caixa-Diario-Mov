using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Repositories;

public class ContaRecorrenteRepository : IContaRecorrenteRepository
{
    private readonly AppDbContext _context;

    public ContaRecorrenteRepository(AppDbContext context) => _context = context;

    public async Task<List<ContaRecorrente>> ListarAtivasPorClienteAsync(Guid clienteId) =>
        await _context.ContasRecorrentes
            .Where(c => c.ClienteId == clienteId && c.Ativo)
            .OrderBy(c => c.CriadoEm)
            .ToListAsync();

    public async Task<ContaRecorrente?> ObterPorIdAsync(Guid clienteId, Guid id) =>
        await _context.ContasRecorrentes
            .FirstOrDefaultAsync(c => c.ClienteId == clienteId && c.Id == id);

    public async Task<ContaRecorrente> AdicionarAsync(ContaRecorrente conta)
    {
        _context.ContasRecorrentes.Add(conta);
        await _context.SaveChangesAsync();
        return conta;
    }

    public async Task<ContaRecorrente> AtualizarAsync(ContaRecorrente conta)
    {
        _context.ContasRecorrentes.Update(conta);
        await _context.SaveChangesAsync();
        return conta;
    }
}
