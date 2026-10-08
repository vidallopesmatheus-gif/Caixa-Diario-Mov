using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Repositories;

public class SugestaoVinculoIgnoradaRepository : ISugestaoVinculoIgnoradaRepository
{
    private readonly AppDbContext _context;

    public SugestaoVinculoIgnoradaRepository(AppDbContext context) => _context = context;

    public async Task<List<SugestaoVinculoIgnorada>> ListarPorClienteAsync(Guid clienteId) =>
        await _context.SugestoesVinculoIgnoradas
            .Where(s => s.ClienteId == clienteId)
            .ToListAsync();

    public async Task AdicionarAsync(SugestaoVinculoIgnorada ignorada)
    {
        _context.SugestoesVinculoIgnoradas.Add(ignorada);
        await _context.SaveChangesAsync();
    }
}
