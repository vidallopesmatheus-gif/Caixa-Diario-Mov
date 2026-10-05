using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Repositories;

public class OcorrenciaRecorrenteDispensadaRepository : IOcorrenciaRecorrenteDispensadaRepository
{
    private readonly AppDbContext _context;

    public OcorrenciaRecorrenteDispensadaRepository(AppDbContext context) => _context = context;

    public async Task<List<OcorrenciaRecorrenteDispensada>> ListarPorClienteAsync(Guid clienteId) =>
        await _context.OcorrenciasRecorrentesDispensadas
            .Where(o => o.ClienteId == clienteId)
            .ToListAsync();

    public async Task<bool> ExisteAsync(Guid recorrenciaId, DateOnly dataVencimento) =>
        await _context.OcorrenciasRecorrentesDispensadas
            .AnyAsync(o => o.RecorrenciaId == recorrenciaId && o.DataVencimento == dataVencimento);

    public async Task AdicionarAsync(OcorrenciaRecorrenteDispensada ocorrencia)
    {
        _context.OcorrenciasRecorrentesDispensadas.Add(ocorrencia);
        await _context.SaveChangesAsync();
    }
}
