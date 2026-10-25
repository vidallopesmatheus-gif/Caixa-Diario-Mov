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
        DateOnly? dataInicio, DateOnly? dataFim, bool? inverterSinaisManualmente = null);

    /// <summary>
    /// Lança as transações do arquivo direto no RegistroDiario (afeta saldo na hora).
    /// dataInicio/dataFim restringem quais linhas do arquivo entram. Deduplicação (FITID ou
    /// heurística data+valor+descrição) roda automaticamente, sem seleção manual do usuário.
    /// Cartão (Bloco 2, Money Vision): quando a conta é CartaoCredito, banco/dataVencimentoFatura
    /// são obrigatórios — o arquivo inteiro vira UMA fatura (nunca mais partida por mês-calendário),
    /// e o sinal das transações é normalizado (compra = saída/dívida, nunca entrando como dinheiro)
    /// por detecção automática de maioria, a menos que inverterSinaisManualmente force um sentido —
    /// mesmo valor usado no preview precisa ser reenviado aqui pra manter a mesma decisão.
    /// </summary>
    Task<ResultadoImportacaoDto> ImportarArquivoAsync(
        Guid contaBancariaId, Guid usuarioLogadoId, string perfil, IFormFile arquivo,
        DateOnly? dataInicio, DateOnly? dataFim, List<ResolucaoDuplicataDto>? resolucoesDuplicatas = null,
        string? banco = null, DateOnly? dataVencimentoFatura = null, DateOnly? dataFechamentoFatura = null,
        string? competenciaFatura = null, bool? inverterSinaisManualmente = null);

    Task<List<PendenteCategorizacaoDto>> ListarPendentesCategorizacaoAsync(
        Guid contaBancariaId, Guid usuarioLogadoId, string perfil);

    Task AtualizarCategoriasAsync(
        Guid contaBancariaId, Guid usuarioLogadoId, string perfil, AtualizarCategoriaDto dto);

    /// <summary>
    /// Exclui um lançamento (Entrada ou Saída) do extrato. Ponta de Transferência exclui as duas
    /// pontas juntas; lançamento vinculado a uma baixa de conta a pagar/receber reabre o título;
    /// se veio de importação, marca a transação de origem como "Ignorada" no histórico só pra
    /// auditoria — não impede reimportar o mesmo arquivo depois.
    /// </summary>
    Task<ExcluirLancamentoResultDto> ExcluirLancamentoAsync(
        Guid contaBancariaId, Guid usuarioLogadoId, string perfil, ExcluirLancamentoDto dto);
}
