using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Repositories;

public class CategoriaRepository : ICategoriaRepository
{
    private readonly AppDbContext _context;

    public CategoriaRepository(AppDbContext context) => _context = context;

    public async Task<List<Categoria>> ListarAtivasAsync() =>
        await _context.Categorias
            .Where(c => c.Ativa)
            .OrderBy(c => c.Ordem)
            .ToListAsync();

    public async Task<List<Categoria>> ListarTodasAsync() =>
        await _context.Categorias
            .OrderBy(c => c.Ordem)
            .ToListAsync();

    public async Task<Categoria?> ObterPorIdAsync(Guid id) =>
        await _context.Categorias.FindAsync(id);

    public async Task<Categoria?> ObterPorNomeAsync(string nome) =>
        await _context.Categorias.FirstOrDefaultAsync(c => c.Nome == nome);

    public async Task<Categoria> AdicionarAsync(Categoria categoria)
    {
        _context.Categorias.Add(categoria);
        await _context.SaveChangesAsync();
        return categoria;
    }

    public async Task<Categoria> AtualizarAsync(Categoria categoria)
    {
        _context.Categorias.Update(categoria);
        await _context.SaveChangesAsync();
        return categoria;
    }

    public async Task RemoverAsync(Categoria categoria)
    {
        _context.Categorias.Remove(categoria);
        await _context.SaveChangesAsync();
    }

    public async Task ReordenarAsync(List<(Guid Id, int Ordem)> novaOrdem)
    {
        var ids = novaOrdem.Select(n => n.Id).ToList();
        var categorias = await _context.Categorias.Where(c => ids.Contains(c.Id)).ToListAsync();
        var ordemPorId = novaOrdem.ToDictionary(n => n.Id, n => n.Ordem);
        foreach (var categoria in categorias)
            categoria.Ordem = ordemPorId[categoria.Id];
        await _context.SaveChangesAsync();
    }

    public async Task<int> ContarUsoAsync(string nome)
    {
        var registros = await _context.RegistrosDiarios.Where(r => !r.Excluido).ToListAsync();
        return registros.Sum(r =>
            r.Entradas.Count(e => e.Categoria == nome) +
            r.Saidas.Count(s => s.Categoria == nome));
    }

    public async Task MigrarUsoAsync(string nomeOrigem, string nomeDestino, string tipoDestino)
    {
        var registros = await _context.RegistrosDiarios.Where(r => !r.Excluido).ToListAsync();
        foreach (var registro in registros)
        {
            var precisaAtualizarEntradas = registro.Entradas.Any(e => e.Categoria == nomeOrigem);
            var precisaAtualizarSaidas = registro.Saidas.Any(s => s.Categoria == nomeOrigem);
            if (!precisaAtualizarEntradas && !precisaAtualizarSaidas) continue;

            if (precisaAtualizarEntradas)
            {
                registro.Entradas = registro.Entradas.Select(e => e.Categoria == nomeOrigem
                    ? new ItemFinanceiro { Descricao = e.Descricao, Valor = e.Valor, Categoria = nomeDestino, TipoCusto = tipoDestino }
                    : e).ToList();
            }
            if (precisaAtualizarSaidas)
            {
                registro.Saidas = registro.Saidas.Select(s => s.Categoria == nomeOrigem
                    ? new ItemFinanceiroSaida { Descricao = s.Descricao, Valor = s.Valor, Categoria = nomeDestino, Subcategoria = s.Subcategoria, TipoCusto = tipoDestino }
                    : s).ToList();
            }
        }
        await _context.SaveChangesAsync();
    }
}
