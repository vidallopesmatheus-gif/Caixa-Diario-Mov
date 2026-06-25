import { apiFetch } from './client'
import type { ApiResponse } from '../types'

export interface ProjecaoItem {
  descricao: string
  valor: number
  categoria?: string
  origem: 'Provisionado' | 'Recorrente'
}

export interface ProjecaoDia {
  data: string
  saldoInicio: number
  entradas: ProjecaoItem[]
  saidas: ProjecaoItem[]
  totalEntradas: number
  totalSaidas: number
  saldoFim: number
  saldoNegativo: boolean
}

export interface Projecao {
  saldoAtual: number
  totalDias: number
  dias: ProjecaoDia[]
}

export async function obterProjecao(
  clienteId: string,
  dias: 30 | 60 | 90,
  contaBancariaId?: string,
): Promise<Projecao> {
  const params = new URLSearchParams({ dias: String(dias) })
  if (contaBancariaId) params.set('contaBancariaId', contaBancariaId)
  const res = await apiFetch<ApiResponse<Projecao>>(
    `/api/projecao/${clienteId}?${params}`,
  )
  return res.dados
}
