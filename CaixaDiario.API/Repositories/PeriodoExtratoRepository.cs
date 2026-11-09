using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Repositories;

public class PeriodoExtratoRepository : IPeriodoExtratoRepository
{
    private readonly AppDbContext _context;

    public PeriodoExtratoRepository(AppDbContext context) => _context = context;

    public async Task<List<PeriodoExtrato>> ListarPorContaAsync(Guid contaBancariaId) =>
        await _context.PeriodosExtrato
            .Where(p => p.ContaBancariaId == contaBancariaId)
            .OrderBy(p => p.De)
            .ToListAsync();

    public async Task AdicionarAsync(PeriodoExtrato periodo)
    {
        if (periodo.Id == Guid.Empty) periodo.Id = Guid.NewGuid();
        _context.PeriodosExtrato.Add(periodo);
        await _context.SaveChangesAsync();
    }
}
