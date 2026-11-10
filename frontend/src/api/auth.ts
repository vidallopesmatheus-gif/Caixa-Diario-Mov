import { apiFetch } from './client'
import type { ApiResponse, LoginResponse } from '../types'

export function login(nomeUsuario: string, senha: string) {
  return apiFetch<ApiResponse<LoginResponse>>('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify({ nomeUsuario, senha }),
  })
}

/** Revoga a sessão (refresh token) no servidor e apaga o cookie. */
export function logout() {
  return apiFetch<ApiResponse<null>>('/api/auth/logout', { method: 'POST' })
}
