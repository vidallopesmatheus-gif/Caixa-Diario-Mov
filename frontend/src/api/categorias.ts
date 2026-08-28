import { apiFetch } from './client'
import type { Categorias, CategoriaAdmin, ApiResponse, TipoCusto } from '../types'

// Module-level cache: categories are static app data, fetched once per session.
let _cache: Categorias | null = null

// Note: /api/categorias returns the payload directly, not wrapped in ApiResponse.
export async function listarCategorias(): Promise<Categorias> {
  if (_cache) return _cache
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const res = await apiFetch<any>('/api/categorias')
  _cache = res as Categorias
  return _cache
}

export function resetCategoriaCache(): void {
  _cache = null
}

export async function listarCategoriasParaGerenciar(): Promise<CategoriaAdmin[]> {
  const res = await apiFetch<ApiResponse<CategoriaAdmin[]>>('/api/categorias/gerenciar')
  return res.dados
}

export async function criarCategoria(nome: string, tipo: TipoCusto): Promise<CategoriaAdmin> {
  const res = await apiFetch<ApiResponse<CategoriaAdmin>>('/api/categorias', {
    method: 'POST',
    body: JSON.stringify({ nome, tipo }),
  })
  resetCategoriaCache()
  return res.dados
}

export async function atualizarCategoria(id: string, nome: string, tipo: TipoCusto, ativa: boolean): Promise<CategoriaAdmin> {
  const res = await apiFetch<ApiResponse<CategoriaAdmin>>(`/api/categorias/${id}`, {
    method: 'PUT',
    body: JSON.stringify({ nome, tipo, ativa }),
  })
  resetCategoriaCache()
  return res.dados
}

export async function desativarCategoria(id: string): Promise<void> {
  await apiFetch<ApiResponse<null>>(`/api/categorias/${id}/desativar`, { method: 'POST' })
  resetCategoriaCache()
}

export async function reordenarCategorias(ids: string[]): Promise<void> {
  await apiFetch<ApiResponse<null>>('/api/categorias/reordenar', {
    method: 'PUT',
    body: JSON.stringify({ ids }),
  })
  resetCategoriaCache()
}

export interface ExclusaoCategoriaResultado {
  excluida: boolean
  quantidadeLancamentos: number
}

/** Tenta excluir; se a categoria estiver em uso, retorna a contagem em vez de lançar erro. */
export async function excluirCategoria(id: string): Promise<ExclusaoCategoriaResultado> {
  const token = localStorage.getItem('token')
  const res = await fetch(`/api/categorias/${id}`, {
    method: 'DELETE',
    headers: token ? { Authorization: `Bearer ${token}` } : {},
  })
  if (res.status !== 200 && res.status !== 409) {
    const err = await res.json().catch(() => ({}))
    throw new Error(err?.mensagem ?? `Erro ${res.status}`)
  }
  const body = await res.json() as ApiResponse<ExclusaoCategoriaResultado>
  resetCategoriaCache()
  return body.dados
}

export async function migrarCategoria(origemId: string, paraCategoriaId: string): Promise<void> {
  await apiFetch<ApiResponse<null>>(`/api/categorias/${origemId}/migrar`, {
    method: 'POST',
    body: JSON.stringify({ paraCategoriaId }),
  })
  resetCategoriaCache()
}
