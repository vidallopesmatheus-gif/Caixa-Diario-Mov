using CaixaDiario.API.DTOs.Categorias;

namespace CaixaDiario.API.Services;

public interface IGrupoService
{
    Task<List<GrupoDto>> ListarTodosAsync();
    Task<GrupoDto> CriarAsync(CriarGrupoDto dto);
    Task<GrupoDto> AtualizarAsync(Guid id, AtualizarGrupoDto dto);
    Task DesativarAsync(Guid id);
    Task ReordenarAsync(ReordenarGruposDto dto);
    Task<string[]> ListarBlocosAsync();
}
