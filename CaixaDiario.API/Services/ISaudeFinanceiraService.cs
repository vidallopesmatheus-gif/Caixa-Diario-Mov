using CaixaDiario.API.DTOs.SaudeFinanceira;
using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

public interface ISaudeFinanceiraService
{
    SaudeFinanceiraDto Calcular(
        List<RegistroDiario> registros,
        List<ContaRecorrente> recorrentes,
        List<MetaAnual> metas);
}
