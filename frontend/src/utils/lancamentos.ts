/**
 * Transferência entre contas e rendimento de investimento não são receita/despesa de
 * negócio — ficam de fora de qualquer soma "de resultado" no frontend (Dashboard),
 * mesmo critério aplicado no backend (CaixaDiario.API/Services/LancamentoFiltro.cs).
 */
export function ehOperacional(item: { tipoCusto?: string }): boolean {
  return item.tipoCusto !== 'Transferencia' && item.tipoCusto !== 'Rendimento'
}
