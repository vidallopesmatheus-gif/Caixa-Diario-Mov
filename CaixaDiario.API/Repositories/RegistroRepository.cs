using CaixaDiario.API.Data;
using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;
using CaixaDiario.API.Services;
using Microsoft.EntityFrameworkCore;

namespace CaixaDiario.API.Repositories;

public class RegistroRepository : IRegistroRepository
{
    private readonly AppDbContext _context;

    public RegistroRepository(AppDbContext context) => _context = context;

    public async Task<RegistroDiario?> ObterPorIdAsync(Guid id) =>
        await _context.RegistrosDiarios.FirstOrDefaultAsync(r => r.Id == id && !r.Excluido);

    public async Task<RegistroDiario?> ObterPorContaEDataAsync(Guid contaBancariaId, DateOnly data) =>
        await _context.RegistrosDiarios
            .FirstOrDefaultAsync(r => r.ContaBancariaId == contaBancariaId && r.Data == data && !r.Excluido);

    public async Task<RegistroDiario?> ObterPorClienteEDataAsync(Guid clienteId, DateOnly data) =>
        await _context.RegistrosDiarios
            .FirstOrDefaultAsync(r => r.ClienteId == clienteId && r.Data == data && !r.Excluido);

    public async Task<RegistroDiario?> ObterPorClienteEDataSemContaAsync(Guid clienteId, DateOnly data) =>
        await _context.RegistrosDiarios
            .FirstOrDefaultAsync(r => r.ClienteId == clienteId && r.Data == data && r.ContaBancariaId == null && !r.Excluido);

    public async Task<List<RegistroDiario>> ListarPorClienteAsync(Guid clienteId) =>
        await _context.RegistrosDiarios
            .Where(r => r.ClienteId == clienteId && !r.Excluido)
            .OrderByDescending(r => r.Data)
            .ToListAsync();

    public async Task<List<RegistroDiario>> ListarPorContaAsync(Guid contaBancariaId) =>
        await _context.RegistrosDiarios
            .Where(r => r.ContaBancariaId == contaBancariaId && !r.Excluido)
            .OrderByDescending(r => r.Data)
            .ToListAsync();

    public async Task<RegistroDiario> AdicionarAsync(RegistroDiario registro)
    {
        await GarantirPeriodoAbertoAsync(registro, novo: true);
        _context.RegistrosDiarios.Add(registro);
        await _context.SaveChangesAsync();
        return registro;
    }

    public async Task<RegistroDiario> AtualizarAsync(RegistroDiario registro)
    {
        await GarantirPeriodoAbertoAsync(registro, novo: false);
        _context.RegistrosDiarios.Update(registro);
        await _context.SaveChangesAsync();
        return registro;
    }

    // Money Vision ("travar o período até o saldo conferido"): mesmo que um caminho esqueça de
    // checar, nada que mude o saldo de um dia travado chega ao banco (categoria pode).
    private async Task GarantirPeriodoAbertoAsync(RegistroDiario depois, bool novo)
    {
        var contas = await _context.ContasBancarias.AsNoTracking()
            .Where(c => c.ClienteId == depois.ClienteId && c.TravadoAte != null).ToListAsync();
        if (contas.Count == 0) return;
        // Estado gravado do dia (consulta nova, sem o que já foi mexido em memória).
        var antes = novo ? null : await _context.RegistrosDiarios.AsNoTracking().FirstOrDefaultAsync(r => r.Id == depois.Id);
        foreach (var conta in contas)
        {
            var travadoAntes = antes != null && antes.ContaBancariaId == conta.Id && PeriodoConferido.Travado(conta, antes.Data);
            var travadoDepois = depois.ContaBancariaId == conta.Id && PeriodoConferido.Travado(conta, depois.Data);
            if (!travadoAntes && !travadoDepois) continue;
            // Dia que saiu da conta/data travada, ou entrou nela: compara cada lado com o vazio.
            var mesmoDia = antes != null && antes.ContaBancariaId == depois.ContaBancariaId && antes.Data == depois.Data;
            var mudou = mesmoDia
                ? PeriodoConferido.MudouOSaldo(antes, depois)
                : (travadoAntes && PeriodoConferido.MudouOSaldo(antes, null)) || (travadoDepois && PeriodoConferido.MudouOSaldo(null, depois));
            if (mudou) throw PeriodoConferido.Erro(conta);
        }
    }

    public async Task<int> ContarTodosPorContaAsync(Guid contaBancariaId) =>
        await _context.RegistrosDiarios.CountAsync(r => r.ContaBancariaId == contaBancariaId);

    public async Task<List<RegistroDiario>> ListarPorPeriodoAsync(Guid clienteId, DateOnly de, DateOnly ate) =>
        await _context.RegistrosDiarios
            .Where(r => r.ClienteId == clienteId && !r.Excluido && r.Data >= de && r.Data <= ate)
            .OrderBy(r => r.Data)
            .ToListAsync();

    public async Task<List<RegistroDiario>> ListarTodosNaoExcluidosAsync() =>
        await _context.RegistrosDiarios.Where(r => !r.Excluido).ToListAsync();
}
