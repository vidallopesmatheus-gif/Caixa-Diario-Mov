using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Repositories;

public class TransacaoImportadaRepository : ITransacaoImportadaRepository
{
    private readonly AppDbContext _context;

    public TransacaoImportadaRepository(AppDbContext context) => _context = context;

    public async Task<List<TransacaoImportada>> ListarPorContaAsync(Guid contaBancariaId) =>
        await _context.TransacoesImportadas
            .Where(t => t.ContaBancariaId == contaBancariaId)
            .ToListAsync();

    public async Task AdicionarLoteAsync(IEnumerable<TransacaoImportada> transacoes)
    {
        _context.TransacoesImportadas.AddRange(transacoes);
        await _context.SaveChangesAsync();
    }
}
