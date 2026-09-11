import { apiFetch } from './client'
import type { ApiResponse, LinkConciliacao } from '../types'

export const gerarLinkConciliacao = async (clienteId: string): Promise<LinkConciliacao> => {
  const res = await apiFetch<ApiResponse<LinkConciliacao>>(`/api/links-conciliacao/${clienteId}/gerar`, {
    method: 'POST',
  })
  return res.dados
}

export const listarLinksConciliacao = async (clienteId: string): Promise<LinkConciliacao[]> => {
  const res = await apiFetch<ApiResponse<LinkConciliacao[]>>(`/api/links-conciliacao/${clienteId}`)
  return res.dados ?? []
}

export const revogarLinkConciliacao = async (id: string): Promise<void> => {
  await apiFetch<ApiResponse<null>>(`/api/links-conciliacao/${id}/revogar`, { method: 'POST' })
}
