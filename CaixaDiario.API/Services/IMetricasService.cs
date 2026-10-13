using CaixaDiario.API.DTOs.Metricas;
using CaixaDiario.API.Models;

namespace CaixaDiario.API.Services;

public interface IMetricasService
{
    // saldoConsolidado: soma do saldo atual de TODAS as contas bancárias ativas do cliente (o
    // mesmo valor exibido em /banco) — nunca derivado de "o SaldoFinal do registro mais recente",
    // que reflete só UMA conta aleatória (a que por acaso tem o lançamento mais recente), não o
    // consolidado. Usado em SaldoProjetado, Runway e Liquidez.
    MetricasPeriodoDto CalcularPeriodo(
        List<RegistroDiario> todosRegistros, List<RegistroDiario> registrosDoPeriodo, decimal saldoConsolidado,
        decimal multiplo = 3m, IReadOnlyList<Categoria>? categorias = null);
    List<EvolucaoMensalDto> CalcularEvolucao(List<RegistroDiario> registros, int meses, DateOnly? ateMesReferencia = null);
    DreDto CalcularDre(List<RegistroDiario> registros, IReadOnlyList<Categoria>? categorias = null);
    // Item 10 do Bloco 5 ("Visão de Futuro"): mesReferencia (null = mês atual) permite que o
    // front caia pro último mês com dados quando o atual ainda está vazio — mesma lógica que já
    // existia na DRE (Item 1.11), agora disponível aqui também.
    IndicadoresDecisaoDto CalcularIndicadores(
        List<RegistroDiario> registros, int mesesEvolucao = 13, IReadOnlyList<Categoria>? categorias = null,
        decimal saldoDisponivel = 0m, DateOnly? mesReferencia = null);
    PontoEquilibrioDetalhadoDto CalcularPontoEquilibrio(decimal receitaBruta, decimal margemContribuicao, decimal despesasFixasTotal, DateOnly diaReferencia);
    List<ResultadoLiquidoMensalDto> CalcularResultadoLiquidoMensal(List<RegistroDiario> registros, DateOnly ateMesReferencia, IReadOnlyList<Categoria>? categorias, int meses = 6);

    // Money Vision — Bloco 5, item 2 da Parte 3: "Para onde foi meu dinheiro" (Entrou → Gastou →
    // Guardou → Ficou). `registrosDoMes` já vem filtrado pro mês desejado; `contasInvestimentoIds`
    // identifica quais linhas de Transferencia contam como "guardado" (aplicação/resgate em
    // investimento), não qualquer transferência entre contas.
    ParaOndeFoiMeuDinheiroDto CalcularParaOndeFoiMeuDinheiro(List<RegistroDiario> registrosDoMes, HashSet<Guid> contasInvestimentoIds);
}
