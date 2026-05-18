import { render, screen, act } from '@testing-library/react'
import { AuthProvider, useAuth } from './AuthContext'
import type { LoginResponse } from '../types'

function Probe() {
  const { user, login, logout } = useAuth()
  return (
    <div>
      <span data-testid="user">{user?.nomeUsuario ?? 'none'}</span>
      <button onClick={() => login({ nomeUsuario: 'novo', perfil: 'admin', nomeCompleto: 'Novo', nomeEstabelecimento: '', usuarioId: '99', token: 'tok99' } as LoginResponse)}>entrar</button>
      <button onClick={logout}>sair</button>
    </div>
  )
}

test('começa sem usuário logado', () => {
  localStorage.clear()
  render(<AuthProvider><Probe /></AuthProvider>)
  expect(screen.getByTestId('user').textContent).toBe('none')
})

test('logout limpa o usuário', async () => {
  localStorage.setItem('token', 'tok')
  localStorage.setItem('user', JSON.stringify({
    nomeUsuario: 'CTI', perfil: 'admin',
    nomeCompleto: 'CTI', nomeEstabelecimento: '',
    usuarioId: '1', token: 'tok'
  }))
  render(<AuthProvider><Probe /></AuthProvider>)
  expect(screen.getByTestId('user').textContent).toBe('CTI')
  await act(async () => { screen.getByText('sair').click() })
  expect(screen.getByTestId('user').textContent).toBe('none')
})

test('login persiste usuário no localStorage e atualiza estado', async () => {
  localStorage.clear()
  render(<AuthProvider><Probe /></AuthProvider>)
  expect(screen.getByTestId('user').textContent).toBe('none')
  await act(async () => { screen.getByText('entrar').click() })
  expect(screen.getByTestId('user').textContent).toBe('novo')
  expect(localStorage.getItem('token')).toBe('tok99')
})

test('inicia com null quando localStorage contém JSON inválido', () => {
  localStorage.setItem('user', 'INVALID_JSON{{{')
  render(<AuthProvider><Probe /></AuthProvider>)
  expect(screen.getByTestId('user').textContent).toBe('none')
})
