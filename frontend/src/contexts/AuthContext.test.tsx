import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { AuthProvider, useAuth } from './AuthContext'
import * as authApi from '../api/auth'
import type { LoginResponse } from '../types'

vi.mock('../api/auth')

// bare localStorage não existe neste ambiente de teste (jsdom/vitest).
function criarStorageFake(): Storage {
  const dados = new Map<string, string>()
  return {
    get length() { return dados.size },
    key: (i: number) => [...dados.keys()][i] ?? null,
    getItem: (k: string) => dados.get(k) ?? null,
    setItem: (k: string, v: string) => { dados.set(k, v) },
    removeItem: (k: string) => { dados.delete(k) },
    clear: () => { dados.clear() },
  }
}

const sessao: LoginResponse = {
  nomeUsuario: 'novo', perfil: 'admin', nomeCompleto: 'Novo', nomeEstabelecimento: '', usuarioId: '99', token: 'tok99', expiraEm: '2026-11-09T13:00:00Z',
}

function Probe() {
  const { user, login, logout } = useAuth()
  return (
    <div>
      <span data-testid="user">{user?.nomeUsuario ?? 'none'}</span>
      <button onClick={() => login(sessao)}>entrar</button>
      <button onClick={logout}>sair</button>
    </div>
  )
}

beforeEach(() => {
  vi.stubGlobal('localStorage', criarStorageFake())
  vi.mocked(authApi.logout).mockResolvedValue({ dados: null } as never)
})
afterEach(() => { vi.unstubAllGlobals() })

describe('AuthProvider', () => {
  it('should start logged out when nothing is saved', () => {
    render(<AuthProvider><Probe /></AuthProvider>)

    expect(screen.getByTestId('user')).toHaveTextContent('none')
  })

  it('should save the user without any token when logging in', async () => {
    render(<AuthProvider><Probe /></AuthProvider>)

    await userEvent.click(screen.getByRole('button', { name: 'entrar' }))

    expect(screen.getByTestId('user')).toHaveTextContent('novo')
    expect(localStorage.getItem('token')).toBeNull()
    expect(localStorage.getItem('user')).not.toContain('tok99')
    expect(JSON.parse(localStorage.getItem('user')!)).toEqual({
      usuarioId: '99', perfil: 'admin', nomeUsuario: 'novo', nomeCompleto: 'Novo', nomeEstabelecimento: '',
    })
  })

  it('should remove the token saved by older versions when the app loads', () => {
    localStorage.setItem('token', 'tok-antigo')
    localStorage.setItem('user', JSON.stringify({ nomeUsuario: 'CTI', perfil: 'admin', nomeCompleto: 'CTI', nomeEstabelecimento: '', usuarioId: '1', token: 'tok-antigo' }))

    render(<AuthProvider><Probe /></AuthProvider>)

    expect(screen.getByTestId('user')).toHaveTextContent('CTI')
    expect(localStorage.getItem('token')).toBeNull()
    expect(localStorage.getItem('user')).not.toContain('tok-antigo')
  })

  it('should revoke the server session and clear the user when logging out', async () => {
    localStorage.setItem('user', JSON.stringify({ nomeUsuario: 'CTI', perfil: 'admin', nomeCompleto: 'CTI', nomeEstabelecimento: '', usuarioId: '1' }))
    render(<AuthProvider><Probe /></AuthProvider>)

    await userEvent.click(screen.getByRole('button', { name: 'sair' }))

    expect(authApi.logout).toHaveBeenCalled()
    expect(screen.getByTestId('user')).toHaveTextContent('none')
    expect(localStorage.getItem('user')).toBeNull()
  })

  it('should start logged out when the saved user is invalid JSON', () => {
    localStorage.setItem('user', 'INVALID_JSON{{{')

    render(<AuthProvider><Probe /></AuthProvider>)

    expect(screen.getByTestId('user')).toHaveTextContent('none')
  })
})
