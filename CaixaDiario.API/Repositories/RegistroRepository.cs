using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Repositories;

public class RegistroRepository : IRegistroRepository
{
    private readonly AppDbContext _context;

    public RegistroRepository(AppDbContext context) => _context = context;

    public async Task<RegistroDiario?> ObterPorClienteEDataAsync(Guid clienteId, DateOnly data) =>
        await _context.RegistrosDiarios
            .FirstOrDefaultAsync(r => r.ClienteId == clienteId && r.Data == data && !r.Excluido);

    public async Task<List<RegistroDiario>> ListarPorClienteAsync(Guid clienteId) =>
        await _context.RegistrosDiarios
            .Where(r => r.ClienteId == clienteId && !r.Excluido)
            .OrderByDescending(r => r.Data)
            .ToListAsync();

    public async Task<RegistroDiario> AdicionarAsync(RegistroDiario registro)
    {
        _context.RegistrosDiarios.Add(registro);
        await _context.SaveChangesAsync();
        return registro;
    }

    public async Task<RegistroDiario> AtualizarAsync(RegistroDiario registro)
    {
        _context.RegistrosDiarios.Update(registro);
        await _context.SaveChangesAsync();
        return registro;
    }
}
