import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { AuthProvider } from '../contexts/AuthContext'
import LoginPage from './LoginPage'
import * as authApi from '../api/auth'

vi.mock('../api/auth')

function renderLogin() {
  return render(
    <MemoryRouter>
      <AuthProvider>
        <LoginPage />
      </AuthProvider>
    </MemoryRouter>
  )
}

test('renderiza campos de usuário e senha', () => {
  renderLogin()
  expect(screen.getByPlaceholderText('seu usuário')).toBeInTheDocument()
  expect(screen.getByPlaceholderText('sua senha')).toBeInTheDocument()
})

test('exibe mensagem de erro quando login falha', async () => {
  vi.mocked(authApi.login).mockRejectedValueOnce(new Error('Usuário ou senha inválidos'))
  renderLogin()
  fireEvent.change(screen.getByPlaceholderText('seu usuário'), { target: { value: 'errado' } })
  fireEvent.change(screen.getByPlaceholderText('sua senha'), { target: { value: '123' } })
  fireEvent.click(screen.getByText('Entrar'))
  await waitFor(() => expect(screen.getByText('Usuário ou senha inválidos')).toBeInTheDocument())
})

test('botão mostra "Entrando..." durante loading', async () => {
  vi.mocked(authApi.login).mockImplementation(() => new Promise(() => {}))
  renderLogin()
  fireEvent.change(screen.getByPlaceholderText('seu usuário'), { target: { value: 'CTI' } })
  fireEvent.change(screen.getByPlaceholderText('sua senha'), { target: { value: '123456' } })
  fireEvent.click(screen.getByText('Entrar'))
  await waitFor(() => expect(screen.getByText('Entrando...')).toBeInTheDocument())
})
