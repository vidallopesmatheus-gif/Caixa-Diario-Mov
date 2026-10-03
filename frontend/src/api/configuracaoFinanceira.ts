import { apiFetch } from './client'
import type { ApiResponse } from '../types'

export interface ConfiguracaoFinanceira {
  custoVidaMensalManual: number | null
  taxaRetiradaFire: number
}

export async function obterConfiguracaoFinanceira(clienteId: string): Promise<ConfiguracaoFinanceira> {
  const res = await apiFetch<ApiResponse<ConfiguracaoFinanceira>>(`/api/configuracao-financeira/${clienteId}`)
  return res.dados
}

export async function atualizarConfiguracaoFinanceira(
  clienteId: string,
  dto: ConfiguracaoFinanceira,
): Promise<ConfiguracaoFinanceira> {
  const res = await apiFetch<ApiResponse<ConfiguracaoFinanceira>>(`/api/configuracao-financeira/${clienteId}`, {
    method: 'PUT',
    body: JSON.stringify(dto),
  })
  return res.dados
}
