using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Repositories;

public class ParcelamentoRepository : IParcelamentoRepository
{
    private readonly AppDbContext _context;

    public ParcelamentoRepository(AppDbContext context) => _context = context;

    public async Task<Parcelamento?> ObterPorIdAsync(Guid id) =>
        await _context.Parcelamentos.FirstOrDefaultAsync(p => p.Id == id);

    public async Task<List<Parcelamento>> ListarPorClienteAsync(Guid clienteId) =>
        await _context.Parcelamentos.Where(p => p.ClienteId == clienteId).ToListAsync();

    public async Task<List<Parcelamento>> ListarAtivosPorContaAsync(Guid contaCartaoId) =>
        await _context.Parcelamentos
            .Where(p => p.ContaCartaoId == contaCartaoId && p.Status == "Ativo")
            .ToListAsync();

    public async Task<Parcelamento> AdicionarAsync(Parcelamento parcelamento)
    {
        _context.Parcelamentos.Add(parcelamento);
        await _context.SaveChangesAsync();
        return parcelamento;
    }

    public async Task<Parcelamento> AtualizarAsync(Parcelamento parcelamento)
    {
        _context.Parcelamentos.Update(parcelamento);
        await _context.SaveChangesAsync();
        return parcelamento;
    }
}
