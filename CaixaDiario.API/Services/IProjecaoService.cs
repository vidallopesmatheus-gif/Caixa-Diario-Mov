using CaixaDiario.API.DTOs.Projecao;
using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

public interface IProjecaoService
{
    ProjecaoDto Calcular(
        List<RegistroDiario> registros,
        List<ContaRecorrente> recorrentes,
        int dias,
        Guid? contaBancariaId);

    TrajetoriaDto CalcularTrajetoria(
        List<RegistroDiario> registros,
        List<ContaRecorrente> recorrentes,
        int mesesPassado,
        int mesesFuturo,
        Guid? contaBancariaId);
}
