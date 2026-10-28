import { apiFetch } from './client'
import type { ApiResponse } from '../types'
import { cachedFetch } from '../utils/requestCache'

export interface OrcamentoDinamico {
  receitaEsperada: number
  compromissosFixos: number
  aporteNecessario: number
  saldoLivre: number
  gastoVariavelAtual: number
  ultrapassado: boolean
  percentualUtilizado: number
}

export async function obterOrcamentoDinamico(clienteId: string): Promise<OrcamentoDinamico> {
  return cachedFetch(`orcamento-dinamico:${clienteId}`, async () => {
    const res = await apiFetch<ApiResponse<OrcamentoDinamico>>(`/api/orcamento-dinamico/${clienteId}`)
    return res.dados!
  })
}
