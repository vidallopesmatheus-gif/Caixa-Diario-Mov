using CaixaDiario.API.DTOs.Categorias;

namespace CaixaDiario.API.Services;

public interface ICategoriaService
{
    Task<CategoriasAgrupadasDto> ListarAgrupadasAsync(Guid clienteId);
    Task<List<CategoriaDto>> ListarParaGerenciarAsync(Guid clienteId, Guid usuarioLogadoId, string perfil);
    Task<CategoriaDto> CriarAsync(Guid clienteId, CriarCategoriaDto dto, Guid usuarioLogadoId, string perfil);
    Task<CategoriaDto> AtualizarAsync(Guid clienteId, Guid id, AtualizarCategoriaDto dto, Guid usuarioLogadoId, string perfil);
    Task DesativarAsync(Guid clienteId, Guid id, Guid usuarioLogadoId, string perfil);
    Task ReordenarAsync(Guid clienteId, ReordenarCategoriasDto dto, Guid usuarioLogadoId, string perfil);
    Task<ExclusaoCategoriaResultDto> ExcluirOuInformarUsoAsync(Guid clienteId, Guid id, Guid usuarioLogadoId, string perfil);
    Task MigrarLancamentosAsync(Guid clienteId, Guid origemId, Guid destinoId, Guid usuarioLogadoId, string perfil);
}
