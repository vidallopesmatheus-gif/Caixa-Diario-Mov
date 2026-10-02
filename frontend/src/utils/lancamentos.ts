/**
 * Transferência entre contas, rendimento de investimento, atividades de investimento/
 * financiamento e pagamento de fatura de cartão não são receita/despesa de negócio — ficam
 * de fora de qualquer soma "de resultado" no frontend (Dashboard, Análise de Padrão), mesmo
 * critério aplicado no backend (CaixaDiario.API/Services/LancamentoFiltro.cs).
 *
 * Nota: até esta correção, este filtro só excluía Transferencia/Rendimento — Investimento/
 * Financiamento entravam por engano nas somas de receita/despesa das telas que usam esta
 * função (Dashboard, Histórico, Admin Overview); e PagamentoFatura nem existia aqui, fazendo
 * fatura de cartão duplicar gastos nos rankings de "Maiores Favorecidos" do DRE.
 */
const TIPOS_NAO_OPERACIONAIS = new Set(['Transferencia', 'Rendimento', 'Investimento', 'Financiamento', 'PagamentoFatura'])

export function ehOperacional(item: { tipoCusto?: string }): boolean {
  return !item.tipoCusto || !TIPOS_NAO_OPERACIONAIS.has(item.tipoCusto)
}
