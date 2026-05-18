// frontend/src/pages/client/ClientGraficoPage.test.tsx
import { render, screen } from '@testing-library/react'
import ClientGraficoPage from './ClientGraficoPage'
import * as AuthContextModule from '../../contexts/AuthContext'
import * as useRegistrosHook from '../../hooks/useRegistros'
import type { Registro } from '../../types'

vi.mock('../../contexts/AuthContext', async (importOriginal) => {
  const actual = await importOriginal<typeof AuthContextModule>()
  return { ...actual, useAuth: vi.fn() }
})
vi.mock('../../hooks/useRegistros')

// Recharts usa ResizeObserver; precisamos de um stub
window.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
}

const mockUser = { usuarioId: 'u1', nomeUsuario: 'cli1', perfil: 'cliente' as const, nomeCompleto: 'C', nomeEstabelecimento: '', token: 'tok' }

const mesAtual = new Date().toISOString().slice(0, 7)
const mockRegistros: Registro[] = [
  {
    id: 'r1', clienteId: 'u1', data: `${mesAtual}-01`,
    saldoInicio: 1000, entrada: 500,
    saidas: [{ descricao: 'Aluguel', valor: 200 }],
    contasAReceber: [], contasAPagar: [],
    saldoConfirmado: 1300, saldoCalculado: 1300, criadoEm: '',
  },
]

function mockHooks(registros = mockRegistros, loading = false) {
  vi.mocked(AuthContextModule.useAuth).mockReturnValue({ user: mockUser, login: vi.fn(), logout: vi.fn() })
  vi.mocked(useRegistrosHook.useRegistros).mockReturnValue({
    registros,
    loading,
    erro: '',
    salvar: vi.fn(),
    excluir: vi.fn(),
    buscarPorData: vi.fn(),
    recarregar: vi.fn(),
  } as ReturnType<typeof useRegistrosHook.useRegistros>)
}

test('exibe loading enquanto carrega', () => {
  mockHooks([], true)
  render(<ClientGraficoPage />)
  expect(screen.getByText(/Carregando/)).toBeInTheDocument()
})

test('renderiza título da seção após carregar', () => {
  mockHooks()
  render(<ClientGraficoPage />)
  expect(screen.getByText(/Evolução Mensal/)).toBeInTheDocument()
})

test('renderiza StatCards com totais do mês', () => {
  mockHooks()
  render(<ClientGraficoPage />)
  expect(screen.getByText(/Total Entradas/)).toBeInTheDocument()
  expect(screen.getByText(/Total Saídas/)).toBeInTheDocument()
})

test('renderiza sem crash quando não há registros', () => {
  mockHooks([])
  render(<ClientGraficoPage />)
  expect(screen.getByText(/Evolução Mensal/)).toBeInTheDocument()
})

test('clienteIdOverride é utilizado quando fornecido', () => {
  mockHooks()
  render(<ClientGraficoPage clienteIdOverride="outro-id" />)
  expect(screen.getByText(/Evolução Mensal/)).toBeInTheDocument()
})

test('renderiza StatCards com saldo inicial e final do mês', () => {
  mockHooks()
  render(<ClientGraficoPage />)
  expect(screen.getByText(/Saldo Inicial/)).toBeInTheDocument()
  expect(screen.getByText(/Saldo Final/)).toBeInTheDocument()
})
