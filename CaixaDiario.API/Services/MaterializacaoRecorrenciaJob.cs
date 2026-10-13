using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

// Parte 2 (integridade de dados): a materialização de ocorrências recorrentes não pode mais
// depender de um GET (nenhuma leitura pode criar/alterar dados) nem só da criação/edição de uma
// recorrente (um cliente pode ficar meses sem tocar nela). Este job roda uma vez ao subir a API e
// depois a cada 24h, materializando 90 dias pra frente pra todo cliente com recorrência ativa —
// idempotente (MaterializarMesAtualAsync já deduplica por conta+data), então rodar de novo depois
// de um restart nunca duplica nada.
public class MaterializacaoRecorrenciaJob : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(24);
    private const int DiasHorizonteFuturo = 90;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MaterializacaoRecorrenciaJob> _logger;

    public MaterializacaoRecorrenciaJob(IServiceScopeFactory scopeFactory, ILogger<MaterializacaoRecorrenciaJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Intervalo);
        do
        {
            await RodarUmaVezAsync(stoppingToken);
        }
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RodarUmaVezAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var contaRepo = scope.ServiceProvider.GetRequiredService<IContaRecorrenteRepository>();
        var recorrenciaService = scope.ServiceProvider.GetRequiredService<IRecorrenciaService>();

        List<Guid> clienteIds;
        try
        {
            clienteIds = (await contaRepo.ListarTodasAsync())
                .Where(c => c.Ativo)
                .Select(c => c.ClienteId)
                .Distinct()
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MaterializacaoRecorrenciaJob: falha ao listar clientes com recorrência ativa.");
            return;
        }

        foreach (var clienteId in clienteIds)
        {
            if (stoppingToken.IsCancellationRequested) return;
            try
            {
                await recorrenciaService.MaterializarMesAtualAsync(clienteId, DiasHorizonteFuturo);
            }
            catch (Exception ex)
            {
                // Um cliente com dado inconsistente não pode travar a materialização dos outros.
                _logger.LogError(ex, "MaterializacaoRecorrenciaJob: falha ao materializar ocorrências do cliente {ClienteId}.", clienteId);
            }
        }
    }
}
