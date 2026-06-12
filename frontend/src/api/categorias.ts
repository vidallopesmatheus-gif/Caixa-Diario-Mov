import { apiFetch } from './client'
import type { Categorias } from '../types'

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
