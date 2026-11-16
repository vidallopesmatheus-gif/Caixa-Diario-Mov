using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Repositories;

public class ConferenciaSaldoRepository : IConferenciaSaldoRepository
{
    private readonly AppDbContext _context;

    public ConferenciaSaldoRepository(AppDbContext context) => _context = context;

    public async Task AdicionarAsync(ConferenciaSaldo conferencia)
    {
        _context.ConferenciasSaldo.Add(conferencia);
        await _context.SaveChangesAsync();
    }

    public async Task<List<ConferenciaSaldo>> ListarPorContaAsync(Guid contaBancariaId) =>
        await _context.ConferenciasSaldo
            .Where(c => c.ContaBancariaId == contaBancariaId)
            .OrderByDescending(c => c.OcorridoEm)
            .ToListAsync();
}
