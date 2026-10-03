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
  /** Só para Ritmo da Meta: "Atingida" | "Adiantado" | "No ritmo" | "Atrasado". */
  statusRitmo?: string | null
  /** Só para Ritmo da Meta: investido − esperado linear até hoje (negativo = atrasado). */
  diferencaReais?: number | null
}

export interface SaudeFinanceira {
  /** Ex.: "Setembro/2026 · último mês fechado" — Taxa de Poupança e Comprometimento Fixo usam
   * sempre o último mês INTEIRO já fechado (não o corrente), independente do período escolhido
   * no topo do Dashboard. */
  periodo: string
  taxaPoupanca: GaugeIndicador
  comprometimentoFixos: GaugeIndicador
  ritmoMeta: GaugeIndicador
}

export async function obterSaudeFinanceira(clienteId: string): Promise<SaudeFinanceira> {
  const res = await apiFetch<ApiResponse<SaudeFinanceira>>(`/api/saude-financeira/${clienteId}`)
  return res.dados!
}
