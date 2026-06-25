using CaixaDiario.API.DTOs.Insights;
using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

public interface IInsightService
{
    List<InsightDto> Calcular(
        List<RegistroDiario> registros,
        List<ContaRecorrente> recorrentes,
        MetaAnual? meta);
}
