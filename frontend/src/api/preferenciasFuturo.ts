import { apiFetch } from './client'
import type { ApiResponse } from '../types'

// Bloco 3 da rodada "Visão de Futuro": preferências só da tela "Meu Futuro" (reserva mínima,
// PF/PJ) — nunca influenciam nenhum outro cálculo do app.
export interface PreferenciasFuturo {
  reservaMinima?: number
  tipoPessoa?: 'PF' | 'PJ'
}

export async function obterPreferenciasFuturo(clienteId: string): Promise<PreferenciasFuturo> {
  const res = await apiFetch<ApiResponse<PreferenciasFuturo>>(`/api/preferencias-futuro/${clienteId}`)
  return res.dados ?? {}
}

export async function atualizarPreferenciasFuturo(
  clienteId: string, dto: PreferenciasFuturo,
): Promise<PreferenciasFuturo> {
  const res = await apiFetch<ApiResponse<PreferenciasFuturo>>(`/api/preferencias-futuro/${clienteId}`, {
    method: 'PUT',
    body: JSON.stringify({ ReservaMinima: dto.reservaMinima, TipoPessoa: dto.tipoPessoa }),
  })
  return res.dados ?? {}
}
