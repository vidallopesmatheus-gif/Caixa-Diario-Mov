using CaixaDiario.API.DTOs.Metricas;
using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

public interface IMetricasService
{
    // saldoConsolidado: soma do saldo atual de TODAS as contas bancárias ativas do cliente (o
    // mesmo valor exibido em /banco) — nunca derivado de "o SaldoFinal do registro mais recente",
    // que reflete só UMA conta aleatória (a que por acaso tem o lançamento mais recente), não o
    // consolidado. Usado em SaldoProjetado, Runway e Liquidez.
    MetricasPeriodoDto CalcularPeriodo(List<RegistroDiario> todosRegistros, List<RegistroDiario> registrosDoPeriodo, decimal saldoConsolidado, decimal multiplo = 3m);
    List<EvolucaoMensalDto> CalcularEvolucao(List<RegistroDiario> registros, int meses);
    DreDto CalcularDre(List<RegistroDiario> registros, IReadOnlyList<Categoria>? categorias = null);
    IndicadoresDecisaoDto CalcularIndicadores(List<RegistroDiario> registros, int mesesEvolucao = 13, IReadOnlyList<Categoria>? categorias = null);
    PontoEquilibrioDetalhadoDto CalcularPontoEquilibrio(decimal receitaBruta, decimal margemContribuicao, decimal despesasFixasTotal, DateOnly diaReferencia);
    List<ResultadoLiquidoMensalDto> CalcularResultadoLiquidoMensal(List<RegistroDiario> registros, DateOnly ateMesReferencia, IReadOnlyList<Categoria>? categorias, int meses = 6);
}
