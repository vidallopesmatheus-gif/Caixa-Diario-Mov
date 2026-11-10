import { createContext, useContext, useState, useCallback, useEffect, type ReactNode } from 'react'
import { aoRenovarSessao, definirTokenAcesso } from '../api/client'
import { logout as apiLogout } from '../api/auth'
import type { LoginResponse } from '../types'

// Money Vision (segurança): o que fica salvo no navegador é só quem é o usuário — o token NUNCA
// (antes ia em localStorage.token e de novo dentro de localStorage.user, lido por qualquer XSS).
type AuthUser = Omit<LoginResponse, 'token' | 'expiraEm'>

interface AuthContextValue {
  user: AuthUser | null
  login: (data: LoginResponse) => void
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

// Campo a campo (nunca "...resto"): o que vai pro localStorage é exatamente isto, e um campo novo
// na resposta do login nunca entra sem querer.
function semToken(dados: AuthUser): AuthUser {
  return {
    usuarioId: dados.usuarioId, perfil: dados.perfil, nomeUsuario: dados.nomeUsuario,
    nomeCompleto: dados.nomeCompleto, nomeEstabelecimento: dados.nomeEstabelecimento,
  }
}

// Sessões salvas antes desta versão guardavam o JWT em dois lugares — remove os dois na carga.
function lerUsuarioSalvo(): AuthUser | null {
  try {
    localStorage.removeItem('token')
    const stored = localStorage.getItem('user')
    if (!stored) return null
    const usuario = semToken(JSON.parse(stored) as AuthUser)
    localStorage.setItem('user', JSON.stringify(usuario))
    return usuario
  } catch {
    return null
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(lerUsuarioSalvo)

  // A renovação silenciosa (api/client.ts) pode trazer nome/perfil atualizados.
  useEffect(() => aoRenovarSessao(sessao => {
    const usuario = semToken(sessao)
    localStorage.setItem('user', JSON.stringify(usuario))
    setUser(usuario)
  }), [])

  const login = useCallback((data: LoginResponse) => {
    definirTokenAcesso(data.token)
    const usuario = semToken(data)
    localStorage.setItem('user', JSON.stringify(usuario))
    setUser(usuario)
  }, [])

  const logout = useCallback(() => {
    // Revoga o refresh token no servidor (e apaga o cookie) — sem isso, "Sair" num computador
    // compartilhado deixava a sessão renovável por mais 14 dias.
    apiLogout().catch(() => {})
    definirTokenAcesso(null)
    localStorage.removeItem('user')
    setUser(null)
  }, [])

  return (
    <AuthContext.Provider value={{ user, login, logout }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be inside AuthProvider')
  return ctx
}
