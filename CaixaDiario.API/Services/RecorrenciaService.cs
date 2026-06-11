using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public class RecorrenciaService : IRecorrenciaService
{
    private readonly IContaRecorrenteRepository _contaRepo;
    private readonly IRegistroRepository _registroRepo;

    public RecorrenciaService(IContaRecorrenteRepository contaRepo, IRegistroRepository registroRepo)
    {
        _contaRepo = contaRepo;
        _registroRepo = registroRepo;
    }

    public async Task MaterializarMesAtualAsync(Guid clienteId)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var primeiroDia = new DateOnly(hoje.Year, hoje.Month, 1);
        var ultimoDia = primeiroDia.AddMonths(1).AddDays(-1);

        var ativas = await _contaRepo.ListarAtivasPorClienteAsync(clienteId);
        if (ativas.Count == 0) return;

        var registrosDoMes = await _registroRepo.ListarPorPeriodoAsync(clienteId, primeiroDia, ultimoDia);

        var materializados = new HashSet<Guid>(
            registrosDoMes.SelectMany(r =>
                r.ContasReceber.Concat(r.ContasPagar)
                    .Where(c => c.RecorrenciaId.HasValue)
                    .Select(c => c.RecorrenciaId!.Value)));

        var pendentes = ativas.Where(c =>
            !materializados.Contains(c.Id) &&
            c.DataInicio <= ultimoDia &&
            (c.DataFim == null || c.DataFim >= primeiroDia)).ToList();

        if (pendentes.Count == 0) return;

        var registroHoje = registrosDoMes.FirstOrDefault(r => r.Data == hoje);
        if (registroHoje == null)
        {
            var todos = await _registroRepo.ListarPorClienteAsync(clienteId);
            var saldoAnterior = todos
                .Where(r => r.Data < hoje)
                .OrderByDescending(r => r.Data)
                .FirstOrDefault()?.SaldoFinal ?? 0m;

            registroHoje = new RegistroDiario
            {
                Id = Guid.NewGuid(),
                ClienteId = clienteId,
                Data = hoje,
                Inicio = saldoAnterior,
                SaldoFinal = saldoAnterior,
                CriadoEm = DateTime.UtcNow,
                SalvoEm = DateTime.UtcNow,
            };
            await _registroRepo.AdicionarAsync(registroHoje);
        }

        foreach (var conta in pendentes)
        {
            var nova = new ContaProvisionada
            {
                Descricao = conta.Descricao,
                Valor = conta.Valor,
                DataVencimento = hoje,
                Pago = false,
                Categoria = conta.Categoria,
                RecorrenciaId = conta.Id,
            };

            if (conta.Tipo == "Receber")
                registroHoje.ContasReceber.Add(nova);
            else
                registroHoje.ContasPagar.Add(nova);
        }

        await _registroRepo.AtualizarAsync(registroHoje);
    }
}
