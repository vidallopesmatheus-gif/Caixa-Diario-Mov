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

export interface TrajetoriaPonto {
  mes: string // "yyyy-MM"
  saldo: number
}

export interface Trajetoria {
  historico: TrajetoriaPonto[]
  projetado: TrajetoriaPonto[]
  mesesHistoricoDisponiveis: number
  saldoAtual: number
  variacaoRealizada: number
  variacaoProjetada: number
}

export async function obterTrajetoria(
  clienteId: string,
  mesesPassado = 6,
  mesesFuturo = 6,
  contaBancariaId?: string,
): Promise<Trajetoria> {
  const params = new URLSearchParams({ mesesPassado: String(mesesPassado), mesesFuturo: String(mesesFuturo) })
  if (contaBancariaId) params.set('contaBancariaId', contaBancariaId)
  const res = await apiFetch<ApiResponse<Trajetoria>>(
    `/api/projecao/${clienteId}/trajetoria?${params}`,
  )
  return res.dados
}
