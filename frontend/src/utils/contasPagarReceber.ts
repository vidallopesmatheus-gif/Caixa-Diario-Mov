import { todayISO } from './format'

// Parte 3 (Money Vision): seletor de mês + "atrasadas sempre no topo" — lógica pura
// compartilhada entre a tela de Contas a pagar/receber e as abas Recebíveis/Pagamentos da
// tela da conta bancária, pra não duplicar a mesma regra em dois lugares.

const MESES_ABREV = ['Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun', 'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez']

export function mesAtualISO(): string {
  return todayISO().slice(0, 7)
}

/** "2026-10" deslocado `delta` meses (negativo = pra trás). */
export function deslocarMes(mes: string, delta: number): string {
  const [ano, m] = mes.split('-').map(Number)
  const indice = ano * 12 + (m - 1) + delta
  const anoNovo = Math.floor(indice / 12)
  const mesNovo = (indice % 12) + 1
  return `${anoNovo}-${String(mesNovo).padStart(2, '0')}`
}

/** "2026-10" → "Out/2026". */
export function labelMes(mes: string): string {
  const [ano, m] = mes.split('-').map(Number)
  return `${MESES_ABREV[m - 1]}/${ano}`
}

/** Dias de atraso de uma pendência (0 = não vencida, não paga ainda ou sem data de vencimento). */
export function calcularDiasAtraso(dataVencimento: string | null | undefined, pago: boolean, hoje = todayISO()): number {
  if (pago || !dataVencimento || dataVencimento >= hoje) return 0
  const dias = Math.round((new Date(hoje + 'T12:00:00').getTime() - new Date(dataVencimento + 'T12:00:00').getTime()) / 86400000)
  return dias > 0 ? dias : 0
}

/** Data de referência de uma pendência pro filtro de mês: vencimento, ou a data do lançamento
 * quando não há vencimento definido. */
export function dataReferencia(dataVencimento: string | null | undefined, dataLancamento: string): string {
  return dataVencimento ?? dataLancamento
}

export function estaNoMes(dataRef: string, mes: string): boolean {
  return dataRef.slice(0, 7) === mes
}
