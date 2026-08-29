using CaixaDiario.API.DTOs.Importacao;
using Microsoft.AspNetCore.Http;

namespace CaixaDiario.API.Services;

public interface IImportacaoService
{
    /// <summary>
    /// Parseia o arquivo e devolve um resumo agregado (quantas novas, quantas já importadas,
    /// totais de entrada/saída) para o intervalo informado — sem persistir nada e sem listar
    /// transação por transação (o usuário confirma "importar tudo", não escolhe linha a linha).
    /// </summary>
    Task<PreviewImportacaoDto> PreviewAsync(
        Guid contaBancariaId, Guid usuarioLogadoId, string perfil, IFormFile arquivo,
        DateOnly? dataInicio, DateOnly? dataFim);

    /// <summary>
    /// Lança as transações do arquivo direto no RegistroDiario (afeta saldo na hora).
    /// dataInicio/dataFim restringem quais linhas do arquivo entram. Deduplicação (FITID ou
    /// heurística data+valor+descrição) roda automaticamente, sem seleção manual do usuário.
    /// </summary>
    Task<ResultadoImportacaoDto> ImportarArquivoAsync(
        Guid contaBancariaId, Guid usuarioLogadoId, string perfil, IFormFile arquivo,
        DateOnly? dataInicio, DateOnly? dataFim);

    Task<List<PendenteCategorizacaoDto>> ListarPendentesCategorizacaoAsync(
        Guid contaBancariaId, Guid usuarioLogadoId, string perfil);

    Task AtualizarCategoriasAsync(
        Guid contaBancariaId, Guid usuarioLogadoId, string perfil, AtualizarCategoriaDto dto);
}
