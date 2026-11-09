using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Repositories;

public class PontoConciliacaoRepository : IPontoConciliacaoRepository
{
    private readonly AppDbContext _context;

    public PontoConciliacaoRepository(AppDbContext context) => _context = context;

    public async Task<List<PontoConciliacao>> ListarPorContaAsync(Guid contaBancariaId) =>
        await _context.PontosConciliacao
            .Where(p => p.ContaBancariaId == contaBancariaId)
            .OrderBy(p => p.Data)
            .ToListAsync();

    public async Task<List<PontoConciliacao>> ListarPorClienteAsync(Guid clienteId) =>
        await _context.PontosConciliacao
            .Where(p => p.ClienteId == clienteId)
            .OrderBy(p => p.Data)
            .ToListAsync();

    public async Task<PontoConciliacao> SalvarAsync(PontoConciliacao ponto)
    {
        var existente = await _context.PontosConciliacao
            .FirstOrDefaultAsync(p => p.ContaBancariaId == ponto.ContaBancariaId && p.Data == ponto.Data);
        if (existente == null)
        {
            if (ponto.Id == Guid.Empty) ponto.Id = Guid.NewGuid();
            ponto.CriadoEm = ponto.AtualizadoEm = DateTime.UtcNow;
            _context.PontosConciliacao.Add(ponto);
            await _context.SaveChangesAsync();
            return ponto;
        }

        existente.SaldoBanco = ponto.SaldoBanco;
        existente.SaldoApp = ponto.SaldoApp;
        existente.Origem = ponto.Origem;
        existente.AtualizadoEm = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return existente;
    }
}
