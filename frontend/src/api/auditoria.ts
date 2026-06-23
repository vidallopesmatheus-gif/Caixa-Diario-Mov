import { apiFetch } from './client'
import type { ApiResponse } from '../types'

export interface AuditLog {
  id: string
  clienteId: string
  usuarioId: string
  entidade: string
  acaoTipo: string
  entidadeId: string
  dadosAntes?: string
  dadosDepois?: string
  ocorridoEm: string
}

export interface AuditLogPaginado {
  items: AuditLog[]
  total: number
  pagina: number
  tamanhoPagina: number
}

export async function listarAuditoria(
  clienteId: string,
  params: { de?: string; ate?: string; entidade?: string; acao?: string; pagina?: number } = {}
): Promise<AuditLogPaginado> {
  const qs = new URLSearchParams()
  if (params.de) qs.set('de', params.de)
  if (params.ate) qs.set('ate', params.ate)
  if (params.entidade) qs.set('entidade', params.entidade)
  if (params.acao) qs.set('acao', params.acao)
  if (params.pagina) qs.set('pagina', String(params.pagina))

  const res = await apiFetch<ApiResponse<AuditLogPaginado>>(`/api/auditoria/${clienteId}?${qs.toString()}`)
  return res.dados
}
