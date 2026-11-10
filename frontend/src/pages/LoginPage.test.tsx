import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter, Routes, Route, useLocation } from 'react-router-dom'
import userEvent from '@testing-library/user-event'
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

// ── Money Vision: sessão expirada ────────────────────────────────────────────────────────────

function renderLoginEm(url: string) {
  return render(
    <MemoryRouter initialEntries={[url]}>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="*" element={<LocalAtual />} />
        </Routes>
      </AuthProvider>
    </MemoryRouter>
  )
}

function LocalAtual() {
  const local = useLocation()
  return <p>Em {local.pathname}{local.search}</p>
}

async function entrarComo(perfil: 'admin' | 'cliente') {
  vi.mocked(authApi.login).mockResolvedValueOnce({
    dados: { token: 't', perfil, nomeUsuario: 'joao', nomeCompleto: 'João', nomeEstabelecimento: '', usuarioId: '1' },
  } as never)
  await userEvent.type(screen.getByPlaceholderText('seu usuário'), 'joao')
  await userEvent.type(screen.getByPlaceholderText('sua senha'), 'senha')
  await userEvent.click(screen.getByRole('button', { name: 'Entrar' }))
}

describe('LoginPage depois de sessão expirada', () => {
  beforeEach(() => { vi.stubGlobal('localStorage', { getItem: () => null, setItem: () => {}, removeItem: () => {} }) })
  afterEach(() => { vi.unstubAllGlobals() })

  it('should warn that the session expired when coming from an expired session', () => {
    renderLoginEm('/login?expirou=1&voltar=%2Fbanco%2Fconta-1')

    expect(screen.getByRole('status')).toHaveTextContent('Sua sessão expirou. Entre de novo para continuar.')
  })

  it('should go back to the original screen after logging in', async () => {
    renderLoginEm('/login?expirou=1&voltar=%2Fbanco%2Fconta-1%3Faba%3Dextrato')

    await entrarComo('cliente')

    expect(await screen.findByText('Em /banco/conta-1?aba=extrato')).toBeInTheDocument()
  })

  it('should ignore a return route outside the app when it points to another site', async () => {
    renderLoginEm('/login?expirou=1&voltar=%2F%2Fsite-malicioso.com')

    await entrarComo('cliente')

    expect(await screen.findByText('Em /caixa')).toBeInTheDocument()
  })
})
