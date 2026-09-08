using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Repositories;

public class GrupoRepository : IGrupoRepository
{
    private readonly AppDbContext _context;

    public GrupoRepository(AppDbContext context) => _context = context;

    public async Task<List<Grupo>> ListarTodosAsync() =>
        await _context.Grupos.OrderBy(g => g.Bloco).ThenBy(g => g.Ordem).ToListAsync();

    public async Task<List<Grupo>> ListarAtivosAsync() =>
        await _context.Grupos.Where(g => g.Ativo).OrderBy(g => g.Bloco).ThenBy(g => g.Ordem).ToListAsync();

    public async Task<Grupo?> ObterPorIdAsync(Guid id) =>
        await _context.Grupos.FindAsync(id);

    public async Task<Grupo?> ObterPorNomeAsync(string nome) =>
        await _context.Grupos.FirstOrDefaultAsync(g => g.Nome == nome);

    public async Task<Grupo> AdicionarAsync(Grupo grupo)
    {
        _context.Grupos.Add(grupo);
        await _context.SaveChangesAsync();
        return grupo;
    }

    public async Task<Grupo> AtualizarAsync(Grupo grupo)
    {
        _context.Grupos.Update(grupo);
        await _context.SaveChangesAsync();
        return grupo;
    }

    public async Task ReordenarAsync(List<(Guid Id, int Ordem)> novaOrdem)
    {
        var ids = novaOrdem.Select(n => n.Id).ToList();
        var grupos = await _context.Grupos.Where(g => ids.Contains(g.Id)).ToListAsync();
        var ordemPorId = novaOrdem.ToDictionary(n => n.Id, n => n.Ordem);
        foreach (var grupo in grupos)
            grupo.Ordem = ordemPorId[grupo.Id];
        await _context.SaveChangesAsync();
    }

    public async Task<int> ContarCategoriasAsync(Guid grupoId) =>
        await _context.Categorias.CountAsync(c => c.GrupoId == grupoId);
}
