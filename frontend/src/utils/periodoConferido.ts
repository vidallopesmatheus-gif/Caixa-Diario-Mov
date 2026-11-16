import { ApiError } from '../api/client'
import { fmtDate } from './format'
import type { ContaBancaria } from '../types'

/** Money Vision ("travar o período até o saldo conferido"): o dia está no período conferido da conta? */
export function diaTravado(conta: Pick<ContaBancaria, 'travadoAte'> | null | undefined, data: string): boolean {
  return !!conta?.travadoAte && data <= conta.travadoAte
}

export function motivoDiaTravado(conta: Pick<ContaBancaria, 'travadoAte'>): string {
  return `Período conferido até ${fmtDate(conta.travadoAte!)} — reabra para editar`
}

export interface ErroPeriodoConferido {
  contaId: string
  conta: string
  travadoAte: string
  peloConsultor: boolean
  mensagem: string
}

/** O 409 PERIODO_CONFERIDO do servidor, com a conta e a data da trava — ou null pra outro erro. */
export function erroPeriodoConferido(e: unknown): ErroPeriodoConferido | null {
  if (!(e instanceof ApiError) || e.codigo !== 'PERIODO_CONFERIDO' || !e.detalhes) return null
  const d = e.detalhes
  return {
    contaId: String(d.contaId), conta: String(d.conta ?? ''), travadoAte: String(d.travadoAte ?? '').slice(0, 10),
    peloConsultor: !!d.peloConsultor, mensagem: e.message,
  }
}
