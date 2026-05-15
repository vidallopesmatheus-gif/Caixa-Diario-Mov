import { render, screen, act } from '@testing-library/react'
import { AuthProvider, useAuth } from './AuthContext'

function Probe() {
  const { user, logout } = useAuth()
  return (
    <div>
      <span data-testid="user">{user?.nomeUsuario ?? 'none'}</span>
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
