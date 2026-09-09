using CaixaDiario.API.DTOs.Metricas;
using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

public interface IMetricasService
{
    MetricasPeriodoDto CalcularPeriodo(List<RegistroDiario> todosRegistros, List<RegistroDiario> registrosDoPeriodo, decimal multiplo = 3m);
    List<EvolucaoMensalDto> CalcularEvolucao(List<RegistroDiario> registros, int meses);
    DreDto CalcularDre(List<RegistroDiario> registros, IReadOnlyList<Categoria>? categorias = null);
    IndicadoresDecisaoDto CalcularIndicadores(List<RegistroDiario> registros, int mesesEvolucao = 13, IReadOnlyList<Categoria>? categorias = null);
    PontoEquilibrioDetalhadoDto CalcularPontoEquilibrio(decimal receitaBruta, decimal margemContribuicao, decimal despesasFixasTotal, DateOnly diaReferencia);
    List<ResultadoLiquidoMensalDto> CalcularResultadoLiquidoMensal(List<RegistroDiario> registros, DateOnly ateMesReferencia, IReadOnlyList<Categoria>? categorias, int meses = 6);
}
