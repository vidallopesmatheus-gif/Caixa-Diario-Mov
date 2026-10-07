import { apiFetch } from './client'
import type { ApiResponse } from '../types'

export interface PontoPrevistoRealizado {
  mes: string // "yyyy-MM"
  previsto: number
  realizado: number
  variacaoPercentual?: number
  variacaoAlta: boolean
  subiuTresMesesSeguidos: boolean
}

export interface LinhaPrevistoRealizado {
  categoria: string
  tipo: 'Receber' | 'Pagar'
  meses: PontoPrevistoRealizado[]
}

export interface PrevistoRealizado {
  linhas: LinhaPrevistoRealizado[]
}

// Fase 1.5: só contas ligadas a uma recorrência entram (são as únicas com um "previsto" de
// verdade pra comparar contra o que aconteceu de fato no extrato).
export async function obterPrevistoRealizado(clienteId: string, meses = 6): Promise<PrevistoRealizado> {
  const res = await apiFetch<ApiResponse<PrevistoRealizado>>(`/api/relatorios/${clienteId}/previsto-realizado?meses=${meses}`)
  return res.dados
}
