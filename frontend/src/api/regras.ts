import { apiFetch } from './client'
import type { ApiResponse, RegraCategorizacao } from '../types'

export const listarRegras = async (clienteId: string): Promise<RegraCategorizacao[]> => {
  const res = await apiFetch<ApiResponse<RegraCategorizacao[]>>(`/api/regras-categorizacao/${clienteId}`)
  return res.dados ?? []
}

export interface CriarRegraDto {
  contaBancariaId: string
  tipo: 'Entrada' | 'Saida'
  descricaoReferencia: string
  acaoTipo: 'Categoria' | 'Transferencia'
  categoria?: string
  contaContrapartidaId?: string
}

export const criarRegra = async (clienteId: string, dto: CriarRegraDto): Promise<RegraCategorizacao> => {
  const res = await apiFetch<ApiResponse<RegraCategorizacao>>(`/api/regras-categorizacao/${clienteId}`, {
    method: 'POST',
    body: JSON.stringify(dto),
  })
  return res.dados
}

export interface AtualizarRegraDto {
  descricaoReferencia: string
  acaoTipo: 'Categoria' | 'Transferencia'
  categoria?: string
  contaContrapartidaId?: string
  ativa: boolean
}

export const atualizarRegra = async (id: string, dto: AtualizarRegraDto): Promise<RegraCategorizacao> => {
  const res = await apiFetch<ApiResponse<RegraCategorizacao>>(`/api/regras-categorizacao/${id}`, {
    method: 'PUT',
    body: JSON.stringify(dto),
  })
  return res.dados
}

export const desativarRegra = async (id: string): Promise<void> => {
  await apiFetch<ApiResponse<null>>(`/api/regras-categorizacao/${id}/desativar`, { method: 'POST' })
}

export const reativarRegra = async (id: string): Promise<void> => {
  await apiFetch<ApiResponse<null>>(`/api/regras-categorizacao/${id}/reativar`, { method: 'POST' })
}

export const excluirRegra = async (id: string): Promise<void> => {
  await apiFetch<ApiResponse<null>>(`/api/regras-categorizacao/${id}`, { method: 'DELETE' })
}

export const reordenarRegras = async (clienteId: string, ids: string[]): Promise<void> => {
  await apiFetch<ApiResponse<null>>(`/api/regras-categorizacao/${clienteId}/reordenar`, {
    method: 'PUT',
    body: JSON.stringify({ ids: ids }),
  })
}

/** Quantos lançamentos pendentes hoje casariam com esse critério — ajuda a validar antes de salvar. */
export const contarCorrespondencias = async (
  contaBancariaId: string, tipo: 'Entrada' | 'Saida', descricaoReferencia: string,
): Promise<number> => {
  const params = new URLSearchParams({ contaBancariaId, tipo, descricaoReferencia })
  const res = await apiFetch<ApiResponse<{ quantidade: number }>>(`/api/regras-categorizacao/contar-correspondencias?${params}`)
  return res.dados?.quantidade ?? 0
}

export const aplicarRegraRetroativamente = async (id: string): Promise<number> => {
  const res = await apiFetch<ApiResponse<{ totalCategorizados: number }>>(`/api/regras-categorizacao/${id}/aplicar-retroativo`, {
    method: 'POST',
  })
  return res.dados?.totalCategorizados ?? 0
}
