using CaixaDiario.API.DTOs.Transferencias;

namespace CaixaDiario.API.Services;

public interface ITransferenciaService
{
    Task<List<TransferenciaDto>> ListarPorClienteAsync(Guid clienteId, Guid usuarioLogadoId, string perfil);
    // Desfaz a classificação como Transferência: reverte os dois lados (categoria/tipo/vínculo)
    // ao estado "sem categoria, pendente" e apaga só o registro de Transferencia — nunca apaga os
    // lançamentos em si, porque eles são dinheiro que realmente se moveu no banco.
    Task DesfazerClassificacaoAsync(Guid id, Guid usuarioLogadoId, string perfil);
    Task<TransferenciaDto> ConverterLancamentoAsync(ConverterLancamentoEmTransferenciaDto dto, Guid usuarioLogadoId, string perfil);
    // Ao contrário de DesfazerClassificacaoAsync, remove de verdade as duas pontas (origem e
    // destino) — usado quando o usuário pede pra EXCLUIR o lançamento, não só reclassificar.
    // As duas pontas são o mesmo evento financeiro: excluir só uma deixaria a outra órfã.
    Task ExcluirAsync(Guid id, Guid usuarioLogadoId, string perfil);
    // Cria as duas pontas do ZERO — diferente de ConverterLancamentoAsync, que sempre relabela um
    // lançamento já real. Usado pelo resgate de investimento da Fase 5 ("🏆 Conquistei"): não existe
    // nenhum lançamento prévio pra converter, as duas pontas nascem junto com a transferência.
    // Provisoria=true nos dois lados — o extrato real da conta de investimento, quando importado,
    // reconcilia com essa saída em vez de duplicar (mesmo mecanismo de ConverterLancamentoAsync).
    Task<TransferenciaDto> CriarAsync(
        Guid clienteId, Guid contaOrigemId, Guid contaDestinoId, DateOnly data, decimal valor, string descricao,
        Guid usuarioLogadoId, string perfil);

    // Money Vision — Bloco 4, item 4 da Parte 2: lançamentos que a importação já suspeita serem
    // transferência (dicionário de palavras-chave) mas que ainda não foram confirmados — "sem par".
    Task<List<SugestaoTransferenciaSemParDto>> ListarSugestoesSemParAsync(Guid clienteId, Guid usuarioLogadoId, string perfil);

    // Remove a sugestão "Transferência" sem confirmar nada — o lançamento volta a ser um pendente
    // comum, pronto pra ser categorizado manualmente como qualquer outro.
    Task DescartarSugestaoAsync(DescartarSugestaoTransferenciaDto dto, Guid usuarioLogadoId, string perfil);
}
