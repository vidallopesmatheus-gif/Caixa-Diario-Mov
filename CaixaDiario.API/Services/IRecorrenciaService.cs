namespace CaixaDiario.API.Services;

public interface IRecorrenciaService
{
    // diasHorizonteFuturo (Parte 2, integridade de dados): além do mês atual, estende quanto à
    // frente materializar — 0 preserva o comportamento histórico (só até o fim do mês atual); o
    // job diário chama com 90, pra cobrir o horizonte pedido mesmo sem ninguém abrir o app.
    Task MaterializarMesAtualAsync(Guid clienteId, int diasHorizonteFuturo = 0);
    // Registra que essa ocorrência específica (não a recorrência inteira) foi excluída pelo
    // cliente — sem isso, a próxima materialização recria ela, porque só enxerga "não existe
    // nos registros atuais" e trata isso como "ainda não foi gerada".
    Task DispensarOcorrenciaAsync(Guid clienteId, Guid recorrenciaId, DateOnly dataVencimento);
}
