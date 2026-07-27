import { apiFetch } from './client'
import type { ApiResponse } from '../types'

export type InsightCategoria = 'saldo' | 'gasto' | 'meta' | 'lucro' | 'geral'

export interface Insight {
  tipo: 'alerta' | 'positivo' | 'neutro'
  texto: string
  detalhe?: string
  prioridade: number
  categoria: InsightCategoria
}

export async function obterInsights(clienteId: string): Promise<Insight[]> {
  const res = await apiFetch<ApiResponse<Insight[]>>(`/api/insights/${clienteId}`)
  return res.dados ?? []
}
