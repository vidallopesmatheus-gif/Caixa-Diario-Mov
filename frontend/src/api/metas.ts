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
    contaInvestimentoId: raw.contaInvestimentoId ?? undefined,
    dataAlvo: raw.dataAlvo ?? undefined,
  }
}

export const obterMeta = async (clienteId: string, ano: number): Promise<MetaAnual | null> => {
  const res = await apiFetch<ApiResponse<unknown>>(`/api/metas/${clienteId}/${ano}`)
  return res.dados ? mapMeta(res.dados) : null
}

export const listarMetas = async (clienteId: string): Promise<MetaAnual[]> => {
  const res = await apiFetch<ApiResponse<unknown[]>>(`/api/metas/${clienteId}`)
  return (res.dados ?? []).map(mapMeta)
}

export const salvarMeta = async (dto: {
  id?: string
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
  dataAlvo?: string
}): Promise<MetaAnual> => {
  const res = await apiFetch<ApiResponse<unknown>>('/api/metas', {
    method: 'POST',
    body: JSON.stringify(dto),
  })
  return mapMeta(res.dados)
}

/** Remove um objetivo (modo "metodo"). A meta de faturamento mensal (modo "simples") não usa isso. */
export const excluirMeta = async (id: string): Promise<void> => {
  await apiFetch<ApiResponse<null>>(`/api/metas/${id}`, { method: 'DELETE' })
}
