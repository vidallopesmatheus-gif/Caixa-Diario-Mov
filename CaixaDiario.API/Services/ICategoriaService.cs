using CaixaDiario.API.DTOs.Categorias;

namespace CaixaDiario.API.Services;

public interface ICategoriaService
{
    Task<CategoriasAgrupadasDto> ListarAgrupadasAsync();
    Task<List<CategoriaDto>> ListarParaGerenciarAsync();
    Task<CategoriaDto> CriarAsync(CriarCategoriaDto dto);
    Task<CategoriaDto> AtualizarAsync(Guid id, AtualizarCategoriaDto dto);
    Task DesativarAsync(Guid id);
    Task ReordenarAsync(ReordenarCategoriasDto dto);
    Task<ExclusaoCategoriaResultDto> ExcluirOuInformarUsoAsync(Guid id);
    Task MigrarLancamentosAsync(Guid origemId, Guid destinoId);
}
