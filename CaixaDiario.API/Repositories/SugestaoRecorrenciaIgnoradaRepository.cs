using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Repositories;

public class SugestaoRecorrenciaIgnoradaRepository : ISugestaoRecorrenciaIgnoradaRepository
{
    private readonly AppDbContext _context;

    public SugestaoRecorrenciaIgnoradaRepository(AppDbContext context) => _context = context;

    public async Task<List<SugestaoRecorrenciaIgnorada>> ListarPorClienteAsync(Guid clienteId) =>
        await _context.SugestoesRecorrenciaIgnoradas
            .Where(s => s.ClienteId == clienteId)
            .ToListAsync();

    public async Task AdicionarAsync(SugestaoRecorrenciaIgnorada ignorada)
    {
        _context.SugestoesRecorrenciaIgnoradas.Add(ignorada);
        await _context.SaveChangesAsync();
    }
}
