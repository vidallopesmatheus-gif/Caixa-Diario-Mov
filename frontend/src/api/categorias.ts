import { apiFetch } from './client'
import type { Categorias, CategoriaAdmin, ApiResponse, Grupo, Bloco } from '../types'

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

export async function criarCategoria(nome: string, grupoId: string): Promise<CategoriaAdmin> {
  const res = await apiFetch<ApiResponse<CategoriaAdmin>>('/api/categorias', {
    method: 'POST',
    body: JSON.stringify({ nome, grupoId }),
  })
  resetCategoriaCache()
  return res.dados
}

export async function atualizarCategoria(id: string, nome: string, grupoId: string, ativa: boolean): Promise<CategoriaAdmin> {
  const res = await apiFetch<ApiResponse<CategoriaAdmin>>(`/api/categorias/${id}`, {
    method: 'PUT',
    body: JSON.stringify({ nome, grupoId, ativa }),
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

// ── Grupos (nível entre Bloco fixo e Categoria) ──────────────────────────────────────────

export async function listarGrupos(): Promise<Grupo[]> {
  const res = await apiFetch<ApiResponse<Grupo[]>>('/api/grupos')
  return res.dados
}

export async function listarBlocos(): Promise<Bloco[]> {
  const res = await apiFetch<ApiResponse<Bloco[]>>('/api/grupos/blocos')
  return res.dados
}

export async function criarGrupo(nome: string, bloco: Bloco): Promise<Grupo> {
  const res = await apiFetch<ApiResponse<Grupo>>('/api/grupos', {
    method: 'POST',
    body: JSON.stringify({ nome, bloco }),
  })
  return res.dados
}

export async function atualizarGrupo(id: string, nome: string, bloco: Bloco, ativo: boolean): Promise<Grupo> {
  const res = await apiFetch<ApiResponse<Grupo>>(`/api/grupos/${id}`, {
    method: 'PUT',
    body: JSON.stringify({ nome, bloco, ativo }),
  })
  resetCategoriaCache()
  return res.dados
}

export async function desativarGrupo(id: string): Promise<void> {
  await apiFetch<ApiResponse<null>>(`/api/grupos/${id}/desativar`, { method: 'POST' })
}

export async function reordenarGrupos(ids: string[]): Promise<void> {
  await apiFetch<ApiResponse<null>>('/api/grupos/reordenar', {
    method: 'PUT',
    body: JSON.stringify({ ids }),
  })
}
