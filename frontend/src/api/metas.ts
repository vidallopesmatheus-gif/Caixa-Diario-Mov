import { apiFetch } from './client'
import type { ApiResponse, MetaAnual } from '../types'

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function mapMeta(raw: any): MetaAnual {
  return {
    id: raw.id,
    clienteId: raw.clienteId,
    ano: raw.ano,
    metaReceita: raw.metaReceita,
    metaLucro: raw.metaLucro,
    mesInicio: raw.mesInicio ?? 1,
    periodoMeses: raw.periodoMeses ?? 12,
    salvoEm: raw.salvoEm ?? new Date().toISOString(),
    sonho: raw.sonho ?? '',
    modoMeta: raw.modoMeta === 'metodo' ? 'metodo' : 'simples',
    valorSonho: raw.valorSonho ?? 0,
    prazoAnos: raw.prazoAnos ?? 0,
    taxaRetorno: raw.taxaRetorno ?? 0,
    totalInvestido: raw.totalInvestido ?? 0,
    margemPJ: raw.margemPJ ?? undefined,
    iconeSonho: raw.iconeSonho ?? undefined,
  }
}

export const obterMeta = async (clienteId: string, ano: number): Promise<MetaAnual | null> => {
  const res = await apiFetch<ApiResponse<unknown>>(`/api/metas/${clienteId}/${ano}`)
  return res.dados ? mapMeta(res.dados) : null
}

export const salvarMeta = async (dto: {
  clienteId: string
  ano: number
  metaReceita: number
  metaLucro: number
  mesInicio: number
  periodoMeses: number
  sonho?: string
  modoMeta?: string
  valorSonho?: number
  prazoAnos?: number
  taxaRetorno?: number
  totalInvestido?: number
  margemPJ?: number
  iconeSonho?: string
}): Promise<MetaAnual> => {
  const res = await apiFetch<ApiResponse<unknown>>('/api/metas', {
    method: 'POST',
    body: JSON.stringify(dto),
  })
  return mapMeta(res.dados)
}
