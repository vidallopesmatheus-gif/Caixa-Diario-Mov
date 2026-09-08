import { apiFetch } from './client'
import type { ApiResponse } from '../types'

export interface GaugeIndicador {
  titulo: string
  valor: number
  valorNormalizado: number
  semaforo: 'verde' | 'amarelo' | 'vermelho' | 'cinza'
  descricao: string
  calculo: string
  disponivel: boolean
}

export interface SaudeFinanceira {
  /** Ex.: "Setembro/2026" — Taxa de Poupança e Comprometimento Fixo são sempre calculados sobre o
   * mês corrente do servidor, não sobre o período escolhido no topo do Dashboard. */
  periodo: string
  taxaPoupanca: GaugeIndicador
  comprometimentoFixos: GaugeIndicador
  ritmoMeta: GaugeIndicador
}

export async function obterSaudeFinanceira(clienteId: string): Promise<SaudeFinanceira> {
  const res = await apiFetch<ApiResponse<SaudeFinanceira>>(`/api/saude-financeira/${clienteId}`)
  return res.dados!
}
