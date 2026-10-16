using CaixaDiario.API.DTOs.Admin;

namespace CaixaDiario.API.Services;

public interface IReclassificacaoCategoriasService
{
    Task<ReclassificacaoCategoriasResultDto> ReclassificarPorUsoAsync(bool confirmar);
}
