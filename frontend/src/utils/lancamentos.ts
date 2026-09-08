/**
 * Transferência entre contas, rendimento de investimento, e atividades de investimento/
 * financiamento não são receita/despesa de negócio — ficam de fora de qualquer soma "de
 * resultado" no frontend (Dashboard), mesmo critério aplicado no backend
 * (CaixaDiario.API/Services/LancamentoFiltro.cs).
 *
 * Nota: até esta correção, este filtro só excluía Transferencia/Rendimento — Investimento/
 * Financiamento entravam por engano nas somas de receita/despesa das telas que usam esta
 * função (Dashboard, Histórico, Admin Overview).
 */
const TIPOS_NAO_OPERACIONAIS = new Set(['Transferencia', 'Rendimento', 'Investimento', 'Financiamento'])

export function ehOperacional(item: { tipoCusto?: string }): boolean {
  return !item.tipoCusto || !TIPOS_NAO_OPERACIONAIS.has(item.tipoCusto)
}
