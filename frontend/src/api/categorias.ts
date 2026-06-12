import { apiFetch } from './client'
import type { Categorias } from '../types'

let _cache: Categorias | null = null

export async function listarCategorias(): Promise<Categorias> {
  if (_cache) return _cache
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const res = await apiFetch<any>('/api/categorias')
  _cache = res as Categorias
  return _cache
}
