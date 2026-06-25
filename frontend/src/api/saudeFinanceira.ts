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
  taxaPoupanca: GaugeIndicador
  comprometimentoFixos: GaugeIndicador
  ritmoMeta: GaugeIndicador
}

export async function obterSaudeFinanceira(clienteId: string): Promise<SaudeFinanceira> {
  const res = await apiFetch<ApiResponse<SaudeFinanceira>>(`/api/saude-financeira/${clienteId}`)
  return res.dados!
}
