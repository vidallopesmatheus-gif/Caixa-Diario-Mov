using CaixaDiario.API.Models;
using CaixaDiario.API.Repositories.Interfaces;

namespace CaixaDiario.API.Services;

public class MetaProgressoService : IMetaProgressoService
{
    private readonly IContaBancariaRepository _contaRepo;
    private readonly IRegistroRepository _registroRepo;

    public MetaProgressoService(IContaBancariaRepository contaRepo, IRegistroRepository registroRepo)
    {
        _contaRepo = contaRepo;
        _registroRepo = registroRepo;
    }

    public async Task AplicarSaldoDeContasVinculadasAsync(Guid clienteId, List<MetaAnual> metas)
    {
        var grupos = metas.Where(m => m.ContaInvestimentoId.HasValue)
            .GroupBy(m => m.ContaInvestimentoId!.Value)
            .ToList();
        if (grupos.Count == 0) return;

        var contas = await _contaRepo.ListarPorClienteAsync(clienteId);
        var registros = await _registroRepo.ListarPorClienteAsync(clienteId);

        foreach (var grupo in grupos)
        {
            var conta = contas.FirstOrDefault(c => c.Id == grupo.Key);
            if (conta == null) continue;

            var saldoConta = ContaBancariaService.ObterSaldoAtual(conta, registros);
            var somaMetas = grupo.Sum(m => m.ValorSonho);
            var percentualCombinado = somaMetas > 0 ? Math.Min(1m, saldoConta / somaMetas) : 0m;

            foreach (var meta in grupo)
                meta.TotalInvestido = meta.ValorSonho * percentualCombinado;
        }
    }
}
