// frontend/src/pages/client/ClientCaixaPage.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import ClientCaixaPage from './ClientCaixaPage'
import * as AuthContextModule from '../../contexts/AuthContext'
import * as useRegistrosHook from '../../hooks/useRegistros'

vi.mock('../../contexts/AuthContext', async (importOriginal) => {
  const actual = await importOriginal<typeof AuthContextModule>()
  return { ...actual, useAuth: vi.fn() }
})
vi.mock('../../hooks/useRegistros')

const mockUser = { usuarioId: 'u1', nomeUsuario: 'cli1', perfil: 'cliente' as const, nomeCompleto: 'Cliente Um', nomeEstabelecimento: '', token: 'tok' }

function mockHooks(overrides: Partial<ReturnType<typeof useRegistrosHook.useRegistros>> = {}) {
  vi.mocked(AuthContextModule.useAuth).mockReturnValue({
    user: mockUser,
    login: vi.fn(),
    logout: vi.fn(),
  })
  vi.mocked(useRegistrosHook.useRegistros).mockReturnValue({
    registros: [],
    loading: false,
    erro: '',
    salvar: vi.fn().mockResolvedValue({}),
    excluir: vi.fn(),
    buscarPorData: vi.fn().mockResolvedValue(null),
    recarregar: vi.fn(),
    ...overrides,
  } as ReturnType<typeof useRegistrosHook.useRegistros>)
}

test('renderiza campos do formulário de caixa', () => {
  mockHooks()
  render(<ClientCaixaPage />)
  expect(screen.getAllByPlaceholderText('0,00').length).toBeGreaterThan(0)
  expect(screen.getByText(/Salvar e sincronizar/)).toBeInTheDocument()
})

test('exibe StatCards com labels corretos', () => {
  mockHooks()
  render(<ClientCaixaPage />)
  expect(screen.getByText('📥 Início')).toBeInTheDocument()
  expect(screen.getByText('📤 Entrada')).toBeInTheDocument()
  expect(screen.getByText('💸 Saídas')).toBeInTheDocument()
  expect(screen.getByText('💰 Saldo')).toBeInTheDocument()
})

test('exibe mensagem de sucesso após salvar', async () => {
  const salvar = vi.fn().mockResolvedValue({})
  mockHooks({ salvar })
  render(<ClientCaixaPage />)
  fireEvent.click(screen.getByText(/Salvar e sincronizar/))
  await waitFor(() => expect(screen.getByText(/Salvo com sucesso/)).toBeInTheDocument())
})

test('exibe mensagem de erro quando salvar falha', async () => {
  const salvar = vi.fn().mockRejectedValue(new Error('Falha de rede'))
  mockHooks({ salvar })
  render(<ClientCaixaPage />)
  fireEvent.click(screen.getByText(/Salvar e sincronizar/))
  await waitFor(() => expect(screen.getByText(/Falha de rede/)).toBeInTheDocument())
})

test('botão fica desabilitado durante salvamento', async () => {
  const salvar = vi.fn().mockImplementation(() => new Promise(() => {}))
  mockHooks({ salvar })
  render(<ClientCaixaPage />)
  fireEvent.click(screen.getByText(/Salvar e sincronizar/))
  await waitFor(() => expect(screen.getByText('Salvando...')).toBeDisabled())
})

test('adiciona nova linha de saída ao clicar em Adicionar saída', () => {
  mockHooks()
  render(<ClientCaixaPage />)
  const antes = screen.getAllByPlaceholderText('Descrição').length
  fireEvent.click(screen.getByText(/Adicionar saída/))
  expect(screen.getAllByPlaceholderText('Descrição')).toHaveLength(antes + 1)
})

test('botões de navegação de dia estão presentes', () => {
  mockHooks()
  render(<ClientCaixaPage />)
  expect(screen.getByText('←')).toBeInTheDocument()
  expect(screen.getByText('→')).toBeInTheDocument()
})
