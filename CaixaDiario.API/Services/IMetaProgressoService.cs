using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

public interface IMetaProgressoService
{
    /// <summary>
    /// Para metas vinculadas a uma conta de investimento (MetaAnual.ContaInvestimentoId),
    /// substitui em memória (não persiste) o TotalInvestido pelo progresso derivado do
    /// saldo real da conta — percentual combinado quando várias metas compartilham a
    /// mesma conta. Metas sem vínculo saem inalteradas (mantém o valor manual).
    /// </summary>
    Task AplicarSaldoDeContasVinculadasAsync(Guid clienteId, List<MetaAnual> metas);
}
