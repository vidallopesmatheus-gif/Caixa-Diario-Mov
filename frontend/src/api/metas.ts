import { apiFetch } from './client'
import type { ApiResponse, MetaAnual } from '../types'

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function mapMeta(raw: any): MetaAnual {
  return { id: raw.id, clienteId: raw.clienteId, ano: raw.ano, metaReceita: raw.metaReceita, metaLucro: raw.metaLucro }
}

export const obterMeta = async (clienteId: string, ano: number): Promise<MetaAnual | null> => {
  try {
    const res = await apiFetch<ApiResponse<unknown>>(`/api/metas/${clienteId}/${ano}`)
    return res.dados ? mapMeta(res.dados) : null
  } catch {
    return null
  }
}

export const salvarMeta = async (dto: { clienteId: string; ano: number; metaReceita: number; metaLucro: number }): Promise<MetaAnual> => {
  const res = await apiFetch<ApiResponse<unknown>>('/api/metas', {
    method: 'POST',
    body: JSON.stringify({ clienteId: dto.clienteId, ano: dto.ano, metaReceita: dto.metaReceita, metaLucro: dto.metaLucro }),
  })
  return mapMeta(res.dados)
}
