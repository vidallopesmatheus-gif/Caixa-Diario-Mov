using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Repositories;

public class DuplicataDispensadaRepository : IDuplicataDispensadaRepository
{
    private readonly AppDbContext _context;

    public DuplicataDispensadaRepository(AppDbContext context) => _context = context;

    public async Task<List<DuplicataDispensada>> ListarPorContaAsync(Guid contaBancariaId) =>
        await _context.DuplicatasDispensadas
            .Where(d => d.ContaBancariaId == contaBancariaId)
            .ToListAsync();

    public async Task AdicionarAsync(DuplicataDispensada dispensada)
    {
        _context.DuplicatasDispensadas.Add(dispensada);
        await _context.SaveChangesAsync();
    }
}
