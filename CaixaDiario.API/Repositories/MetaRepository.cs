using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Repositories;

public class MetaRepository : IMetaRepository
{
    private readonly AppDbContext _context;

    public MetaRepository(AppDbContext context) => _context = context;

    public async Task<MetaAnual?> ObterMetaSimplesPorClienteEAnoAsync(Guid clienteId, int ano) =>
        await _context.MetasAnuais.FirstOrDefaultAsync(m => m.ClienteId == clienteId && m.Ano == ano && m.ModoMeta == "simples");

    public async Task<MetaAnual?> ObterPorIdAsync(Guid id) =>
        await _context.MetasAnuais.FindAsync(id);

    public async Task<List<MetaAnual>> ListarPorClienteAsync(Guid clienteId) =>
        await _context.MetasAnuais.Where(m => m.ClienteId == clienteId).ToListAsync();

    public async Task<MetaAnual> SalvarAsync(MetaAnual meta)
    {
        var existente = await _context.MetasAnuais.FindAsync(meta.Id);
        if (existente == null)
            _context.MetasAnuais.Add(meta);
        else
            _context.MetasAnuais.Update(meta);
        await _context.SaveChangesAsync();
        return meta;
    }

    public async Task RemoverAsync(MetaAnual meta)
    {
        _context.MetasAnuais.Remove(meta);
        await _context.SaveChangesAsync();
    }
}
