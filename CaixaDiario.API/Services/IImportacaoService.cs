using CaixaDiario.API.DTOs.Importacao;
using Microsoft.AspNetCore.Http;

namespace CaixaDiario.API.Services;

public interface IImportacaoService
{
    /// <summary>Parseia o arquivo e devolve as transações encontradas, sem persistir nada.</summary>
    Task<PreviewImportacaoDto> PreviewAsync(
        Guid contaBancariaId, Guid usuarioLogadoId, string perfil, IFormFile arquivo);

    /// <summary>
    /// Lança as transações do arquivo direto no RegistroDiario (afeta saldo na hora).
    /// dataInicio/dataFim restringem quais linhas do arquivo entram; indicesForcarInclusao
    /// inclui mesmo assim transações que o preview marcou como já importadas.
    /// </summary>
    Task<ResultadoImportacaoDto> ImportarArquivoAsync(
        Guid contaBancariaId, Guid usuarioLogadoId, string perfil, IFormFile arquivo,
        DateOnly? dataInicio, DateOnly? dataFim, List<int>? indicesForcarInclusao);

    Task<List<PendenteCategorizacaoDto>> ListarPendentesCategorizacaoAsync(
        Guid contaBancariaId, Guid usuarioLogadoId, string perfil);

    Task AtualizarCategoriasAsync(
        Guid contaBancariaId, Guid usuarioLogadoId, string perfil, AtualizarCategoriaDto dto);
}
