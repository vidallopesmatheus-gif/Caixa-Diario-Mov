using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Repositories;

public class ContaBancariaRepository : IContaBancariaRepository
{
    private readonly AppDbContext _context;

    public ContaBancariaRepository(AppDbContext context) => _context = context;

    public async Task<List<ContaBancaria>> ListarPorClienteAsync(Guid clienteId) =>
        await _context.ContasBancarias
            .Where(c => c.ClienteId == clienteId)
            .OrderBy(c => c.DataCriacao)
            .ToListAsync();

    public async Task<ContaBancaria?> ObterPorIdAsync(Guid id) =>
        await _context.ContasBancarias.FindAsync(id);

    public async Task<ContaBancaria?> ObterCaixaPadraoAsync(Guid clienteId) =>
        await _context.ContasBancarias
            .Where(c => c.ClienteId == clienteId && c.Tipo == "Caixa" && c.Ativa)
            .OrderBy(c => c.DataCriacao)
            .FirstOrDefaultAsync();

    public async Task<ContaBancaria> AdicionarAsync(ContaBancaria conta)
    {
        _context.ContasBancarias.Add(conta);
        await _context.SaveChangesAsync();
        return conta;
    }

    public async Task<ContaBancaria> AtualizarAsync(ContaBancaria conta)
    {
        _context.ContasBancarias.Update(conta);
        await _context.SaveChangesAsync();
        return conta;
    }
}
