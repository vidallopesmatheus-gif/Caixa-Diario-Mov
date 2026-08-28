using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Repositories;

public class TransferenciaRepository : ITransferenciaRepository
{
    private readonly AppDbContext _context;

    public TransferenciaRepository(AppDbContext context) => _context = context;

    public async Task<Transferencia?> ObterPorIdAsync(Guid id) =>
        await _context.Transferencias.FindAsync(id);

    public async Task<List<Transferencia>> ListarPorClienteAsync(Guid clienteId) =>
        await _context.Transferencias
            .Where(t => t.ClienteId == clienteId)
            .OrderByDescending(t => t.Data)
            .ThenByDescending(t => t.CriadoEm)
            .ToListAsync();

    public async Task<Transferencia> AdicionarAsync(Transferencia transferencia)
    {
        _context.Transferencias.Add(transferencia);
        await _context.SaveChangesAsync();
        return transferencia;
    }

    public async Task RemoverAsync(Transferencia transferencia)
    {
        _context.Transferencias.Remove(transferencia);
        await _context.SaveChangesAsync();
    }
}
