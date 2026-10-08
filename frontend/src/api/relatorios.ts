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

// Fase 1.5: por padrão só contas ligadas a uma recorrência entram (são as únicas com um "previsto"
// de verdade pra comparar contra o extrato). Fase 1.10: incluirAvulsosVinculados também traz
// títulos avulsos cuja baixa foi vinculada a um lançamento já existente — têm uma expectativa real
// (o valor provisionado), só que opcional porque mistura título recorrente com avulso na mesma linha.
export async function obterPrevistoRealizado(
  clienteId: string, meses = 6, incluirAvulsosVinculados = false,
): Promise<PrevistoRealizado> {
  const res = await apiFetch<ApiResponse<PrevistoRealizado>>(
    `/api/relatorios/${clienteId}/previsto-realizado?meses=${meses}&incluirAvulsosVinculados=${incluirAvulsosVinculados}`,
  )
  return res.dados
}
