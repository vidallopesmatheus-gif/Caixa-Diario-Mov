using CaixaDiario.API.DTOs.OrcamentoDinamico;
using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

public interface IOrcamentoDinamicoService
{
    OrcamentoDinamicoDto Calcular(
        List<RegistroDiario> registros,
        List<ContaRecorrente> recorrentes,
        List<MetaAnual> metas);
}
