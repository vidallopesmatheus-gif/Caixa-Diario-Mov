// frontend/src/pages/client/ClientGraficoPage.test.tsx
import { render, screen, waitFor } from '@testing-library/react'
import ClientGraficoPage from './ClientGraficoPage'
import * as AuthContextModule from '../../contexts/AuthContext'
import * as metricasApi from '../../api/metricas'
import * as useRegistrosModule from '../../hooks/useRegistros'
import type { EvolucaoMensal } from '../../api/metricas'

vi.mock('../../contexts/AuthContext', async (importOriginal) => {
  const actual = await importOriginal<typeof AuthContextModule>()
  return { ...actual, useAuth: vi.fn() }
})
vi.mock('../../api/metricas', async (importOriginal) => {
  const actual = await importOriginal<typeof metricasApi>()
  return { ...actual, obterEvolucao: vi.fn() }
})
vi.mock('../../hooks/useRegistros', async (importOriginal) => {
  const actual = await importOriginal<typeof useRegistrosModule>()
  return { ...actual, useRegistros: vi.fn() }
})

// Recharts usa ResizeObserver; precisamos de um stub
window.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
}

const mockUser = { usuarioId: 'u1', nomeUsuario: 'cli1', perfil: 'cliente' as const, nomeCompleto: 'C', nomeEstabelecimento: '', token: 'tok' }

const mockEvolucao: EvolucaoMensal[] = [
  { mes: '2026-01', receita: 5000, custos: 2000, lucro: 3000, saldo: 10000 },
  { mes: '2026-02', receita: 6000, custos: 2500, lucro: 3500, saldo: 13500 },
]

const mockRegistrosReturn = {
  registros: [],
  loading: false,
  erro: '',
  salvar: vi.fn(),
  excluir: vi.fn(),
  buscarPorData: vi.fn(),
  recarregar: vi.fn(),
}

function mockHooks(evolucao: EvolucaoMensal[] = mockEvolucao) {
  vi.mocked(AuthContextModule.useAuth).mockReturnValue({ user: mockUser, login: vi.fn(), logout: vi.fn() })
  vi.mocked(metricasApi.obterEvolucao).mockResolvedValue(evolucao)
  vi.mocked(useRegistrosModule.useRegistros).mockReturnValue(mockRegistrosReturn)
}

test('exibe loading enquanto carrega', () => {
  vi.mocked(AuthContextModule.useAuth).mockReturnValue({ user: mockUser, login: vi.fn(), logout: vi.fn() })
  vi.mocked(metricasApi.obterEvolucao).mockReturnValue(new Promise(() => {})) // never resolves
  vi.mocked(useRegistrosModule.useRegistros).mockReturnValue(mockRegistrosReturn)
  render(<ClientGraficoPage />)
  expect(screen.getByText(/Carregando/)).toBeInTheDocument()
})

test('renderiza título da seção após carregar', async () => {
  mockHooks()
  render(<ClientGraficoPage />)
  await waitFor(() => expect(screen.getByText(/Receita vs\. Custos/)).toBeInTheDocument())
})

test('renderiza StatCards com totais do período', async () => {
  mockHooks()
  render(<ClientGraficoPage />)
  await waitFor(() => {
    expect(screen.getByText(/Receita Total/)).toBeInTheDocument()
    expect(screen.getByText(/Custos Totais/)).toBeInTheDocument()
  })
})

test('renderiza sem crash quando não há registros', async () => {
  mockHooks([])
  render(<ClientGraficoPage />)
  await waitFor(() => expect(screen.getByText(/Receita vs\. Custos/)).toBeInTheDocument())
})

test('clienteIdOverride é utilizado quando fornecido', async () => {
  mockHooks()
  render(<ClientGraficoPage clienteIdOverride="outro-id" />)
  await waitFor(() => expect(screen.getByText(/Receita vs\. Custos/)).toBeInTheDocument())
  expect(vi.mocked(metricasApi.obterEvolucao)).toHaveBeenCalledWith('outro-id', 6)
})

test('renderiza StatCards com lucro e saldo atual', async () => {
  mockHooks()
  render(<ClientGraficoPage />)
  await waitFor(() => {
    expect(screen.getByText(/Lucro Total/)).toBeInTheDocument()
    expect(screen.getByText(/Saldo Atual/)).toBeInTheDocument()
  })
})
