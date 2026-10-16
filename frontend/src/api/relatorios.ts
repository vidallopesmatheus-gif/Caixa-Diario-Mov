import { apiFetch } from './client'
import type { ApiResponse } from '../types'

// Item 7 da rodada de ajustes Money Vision: "Previsto × Realizado" por ITEM (favorecido), não mais
// por categoria — uma categoria como "Salários/Folha" mistura vários favorecidos diferentes.
export type StatusPrevistoRealizado = 'Realizado' | 'Parcial' | 'NaoAconteceu' | 'Atrasado'

export interface ItemPrevistoRealizado {
  chave: string
  nome: string
  tipo: 'Receber' | 'Pagar'
  categoria?: string
  previsto: number
  realizado: number
  diferenca: number
  diferencaPercentual?: number
  status: StatusPrevistoRealizado
  dataVencimento?: string
}

export interface PontoAcumuladoPrevistoRealizado {
  // "yyyy-MM-dd" (mês atual, meses=1) ou "yyyy-MM" (3/6/12 meses).
  data: string
  previstoAcumulado: number
  realizadoAcumulado: number
}

export interface PrevistoRealizado {
  itens: ItemPrevistoRealizado[]
  acumuladoReceber: PontoAcumuladoPrevistoRealizado[]
  acumuladoPagar: PontoAcumuladoPrevistoRealizado[]
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

export interface PontoHistoricoPrevistoRealizado {
  mes: string // "yyyy-MM"
  previsto: number
  realizado: number
  variacaoPercentual?: number
}

export interface HistoricoItemPrevistoRealizado {
  nome: string
  meses: PontoHistoricoPrevistoRealizado[]
}

// Item 7, ponto 5: "tocar num item mostra o histórico dele" — previsto × realizado dos últimos
// meses, só desse favorecido (mesma chave vinda de ItemPrevistoRealizado.chave).
export async function obterHistoricoItemPrevistoRealizado(
  clienteId: string, chave: string, meses = 6, incluirAvulsosVinculados = false,
): Promise<HistoricoItemPrevistoRealizado> {
  const res = await apiFetch<ApiResponse<HistoricoItemPrevistoRealizado>>(
    `/api/relatorios/${clienteId}/previsto-realizado/historico?chave=${encodeURIComponent(chave)}&meses=${meses}&incluirAvulsosVinculados=${incluirAvulsosVinculados}`,
  )
  return res.dados
}
