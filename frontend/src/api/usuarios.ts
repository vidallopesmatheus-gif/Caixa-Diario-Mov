import { apiFetch } from './client'
import type { ApiResponse, Usuario } from '../types'

export const listarUsuarios = () =>
  apiFetch<ApiResponse<Usuario[]>>('/api/usuarios')

export const criarUsuario = (dto: {
  nomeUsuario: string
  senha: string
  nomeCompleto: string
  nomeEstabelecimento: string
  perfil: string
}) =>
  apiFetch<ApiResponse<Usuario>>('/api/usuarios', {
    method: 'POST',
    body: JSON.stringify(dto),
  })

export const atualizarUsuario = (
  id: string,
  dto: { nomeCompleto: string; nomeEstabelecimento: string; senha?: string }
) =>
  apiFetch<ApiResponse<Usuario>>(`/api/usuarios/${id}`, {
    method: 'PUT',
    body: JSON.stringify(dto),
  })

export const desativarUsuario = (id: string) =>
  apiFetch<ApiResponse<null>>(`/api/usuarios/${id}`, { method: 'DELETE' })
