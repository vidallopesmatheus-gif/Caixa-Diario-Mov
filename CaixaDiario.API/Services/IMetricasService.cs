using CaixaDiario.API.DTOs.Metricas;
using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

public interface IMetricasService
{
    MetricasPeriodoDto CalcularPeriodo(List<RegistroDiario> todosRegistros, List<RegistroDiario> registrosDoPeriodo, decimal multiplo = 3m);
    List<EvolucaoMensalDto> CalcularEvolucao(List<RegistroDiario> registros, int meses);
    FluxoProjetadoDto CalcularFluxoProjetado(List<RegistroDiario> registros, List<ContaRecorrente> recorrentes, int dias);
    DreDto CalcularDre(List<RegistroDiario> registros);
}
