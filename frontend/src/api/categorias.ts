import { apiFetch } from './client'
import type { Categorias, CategoriaAdmin, ApiResponse, Grupo, Bloco, TipoMovimento } from '../types'

// Correção de integridade (Parte 1): categoria deixou de ser global — o cache agora é por
// cliente (nunca compartilhado entre clientes), e é invalidado sempre que o cliente muda.
let _cacheClienteId: string | null = null
let _cache: Categorias | null = null

// Note: /api/clientes/{clienteId}/categorias retorna o payload direto, não embrulhado em ApiResponse.
export async function listarCategorias(clienteId: string): Promise<Categorias> {
  if (_cache && _cacheClienteId === clienteId) return _cache
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const res = await apiFetch<any>(`/api/clientes/${clienteId}/categorias`)
  _cache = res as Categorias
  _cacheClienteId = clienteId
  return _cache
}

export function resetCategoriaCache(): void {
  _cache = null
  _cacheClienteId = null
}

export async function listarCategoriasParaGerenciar(clienteId: string): Promise<CategoriaAdmin[]> {
  const res = await apiFetch<ApiResponse<CategoriaAdmin[]>>(`/api/clientes/${clienteId}/categorias/gerenciar`)
  return res.dados
}

export async function criarCategoria(
  clienteId: string, nome: string, grupoId: string, tipoMovimento?: TipoMovimento,
): Promise<CategoriaAdmin> {
  const res = await apiFetch<ApiResponse<CategoriaAdmin>>(`/api/clientes/${clienteId}/categorias`, {
    method: 'POST',
    body: JSON.stringify({ nome, grupoId, tipoMovimento }),
  })
  resetCategoriaCache()
  return res.dados
}

export async function atualizarCategoria(
  clienteId: string, id: string, nome: string, grupoId: string, ativa: boolean, tipoMovimento?: TipoMovimento,
): Promise<CategoriaAdmin> {
  const res = await apiFetch<ApiResponse<CategoriaAdmin>>(`/api/clientes/${clienteId}/categorias/${id}`, {
    method: 'PUT',
    body: JSON.stringify({ nome, grupoId, ativa, tipoMovimento }),
  })
  resetCategoriaCache()
  return res.dados
}

export async function desativarCategoria(clienteId: string, id: string): Promise<void> {
  await apiFetch<ApiResponse<null>>(`/api/clientes/${clienteId}/categorias/${id}/desativar`, { method: 'POST' })
  resetCategoriaCache()
}

export async function reordenarCategorias(clienteId: string, ids: string[]): Promise<void> {
  await apiFetch<ApiResponse<null>>(`/api/clientes/${clienteId}/categorias/reordenar`, {
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
export async function excluirCategoria(clienteId: string, id: string): Promise<ExclusaoCategoriaResultado> {
  const token = localStorage.getItem('token')
  const res = await fetch(`/api/clientes/${clienteId}/categorias/${id}`, {
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

export async function migrarCategoria(clienteId: string, origemId: string, paraCategoriaId: string): Promise<void> {
  await apiFetch<ApiResponse<null>>(`/api/clientes/${clienteId}/categorias/${origemId}/migrar`, {
    method: 'POST',
    body: JSON.stringify({ paraCategoriaId }),
  })
  resetCategoriaCache()
}

// ── Grupos (nível entre Bloco fixo e Categoria) — seguem globais (molde do Plano de Contas,
// compartilhado por todos os clientes; só a Categoria dentro de cada Grupo é por cliente). ──────

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
