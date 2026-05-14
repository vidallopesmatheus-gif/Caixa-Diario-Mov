using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Repositories;

public class UsuarioRepository : IUsuarioRepository
{
    private readonly AppDbContext _context;

    public UsuarioRepository(AppDbContext context) => _context = context;

    public async Task<Usuario?> ObterPorIdAsync(Guid id) =>
        await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == id);

    public async Task<Usuario?> ObterPorNomeUsuarioAsync(string nomeUsuario) =>
        await _context.Usuarios.FirstOrDefaultAsync(u => u.NomeUsuario == nomeUsuario);

    public async Task<List<Usuario>> ListarAtivosAsync() =>
        await _context.Usuarios.Where(u => u.Ativo).ToListAsync();

    public async Task<bool> ExisteNomeUsuarioAsync(string nomeUsuario, Guid? excluirId = null)
    {
        var query = _context.Usuarios.Where(u => u.NomeUsuario == nomeUsuario);
        if (excluirId.HasValue)
            query = query.Where(u => u.Id != excluirId.Value);
        return await query.AnyAsync();
    }

    public async Task<Usuario> AdicionarAsync(Usuario usuario)
    {
        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();
        return usuario;
    }

    public async Task<Usuario> AtualizarAsync(Usuario usuario)
    {
        _context.Usuarios.Update(usuario);
        await _context.SaveChangesAsync();
        return usuario;
    }
}
