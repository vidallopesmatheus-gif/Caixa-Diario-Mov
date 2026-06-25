using CaixaDiario.API.DTOs.Importacao;
using Microsoft.AspNetCore.Http;

namespace CaixaDiario.API.Services;

public interface IImportacaoService
{
    Task<List<TransacaoImportadaDto>> ImportarArquivoAsync(
        Guid contaBancariaId, Guid usuarioLogadoId, string perfil, IFormFile arquivo);

    Task<List<TransacaoImportadaDto>> ListarPendentesAsync(
        Guid contaBancariaId, Guid usuarioLogadoId, string perfil);

    Task ConfirmarTransacoesAsync(
        Guid contaBancariaId, Guid usuarioLogadoId, string perfil, ConfirmarTransacoesDto dto);
}
