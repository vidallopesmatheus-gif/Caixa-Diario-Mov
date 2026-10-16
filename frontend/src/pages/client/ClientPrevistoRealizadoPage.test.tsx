import { render, screen, waitFor } from '@testing-library/react'
import ClientPrevistoRealizadoPage from './ClientPrevistoRealizadoPage'
import * as AuthContextModule from '../../contexts/AuthContext'
import * as relatoriosApi from '../../api/relatorios'
import type { PrevistoRealizado } from '../../api/relatorios'

vi.mock('../../contexts/AuthContext', async (importOriginal) => {
  const actual = await importOriginal<typeof AuthContextModule>()
  return { ...actual, useAuth: vi.fn() }
})
vi.mock('../../api/relatorios')

// Recharts usa ResizeObserver; precisamos de um stub
window.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
}

const mockUser = { usuarioId: 'u1', nomeUsuario: 'cli1', perfil: 'cliente' as const, nomeCompleto: 'C', nomeEstabelecimento: '', token: 'tok' }

function mockHooks(dados: PrevistoRealizado) {
  vi.mocked(AuthContextModule.useAuth).mockReturnValue({ user: mockUser, login: vi.fn(), logout: vi.fn() })
  vi.mocked(relatoriosApi.obterPrevistoRealizado).mockResolvedValue(dados)
}

// Rodada de ajustes Money Vision (item 7): "Previsto × Realizado" por ITEM (favorecido), com a tela
// por padrão mostrando o filtro "Só entradas" (tipo Receber) — cobertura adaptada a esse formato.
test('mostra a lista de itens quando há dados', async () => {
  mockHooks({
    itens: [
      {
        chave: 'Receber:CLIENTE A', nome: 'Cliente A', tipo: 'Receber', categoria: 'Vendas',
        previsto: 1000, realizado: 1050, diferenca: 50, diferencaPercentual: 0.05, status: 'Parcial', dataVencimento: '2026-10-05',
      },
    ],
    acumuladoReceber: [],
    acumuladoPagar: [],
  })

  render(<ClientPrevistoRealizadoPage />)

  await waitFor(() => expect(screen.getAllByText('Cliente A').length).toBeGreaterThan(0))
  expect(document.querySelector('.mv-bullet-list')).toBeInTheDocument()
})

test('soma previsto e realizado dos itens visíveis, nos KPIs', async () => {
  mockHooks({
    itens: [
      {
        chave: 'Receber:CLIENTE A', nome: 'Cliente A', tipo: 'Receber', categoria: 'Vendas',
        previsto: 1000, realizado: 1050, diferenca: 50, diferencaPercentual: 0.05, status: 'Parcial', dataVencimento: '2026-10-05',
      },
      {
        chave: 'Receber:CLIENTE B', nome: 'Cliente B', tipo: 'Receber', categoria: 'Vendas',
        previsto: 200, realizado: 250, diferenca: 50, diferencaPercentual: 0.25, status: 'Parcial', dataVencimento: '2026-10-10',
      },
    ],
    acumuladoReceber: [],
    acumuladoPagar: [],
  })

  render(<ClientPrevistoRealizadoPage />)

  await waitFor(() => expect(screen.getAllByText('Cliente A').length).toBeGreaterThan(0))
  // Soma dos dois itens (tipo Receber, filtro padrão): previsto 1000+200, realizado 1050+250.
  expect(screen.getByText('R$ 1.200,00')).toBeInTheDocument()
  expect(screen.getByText('R$ 1.300,00')).toBeInTheDocument()
})

test('sem dados, não mostra a lista de itens', async () => {
  mockHooks({ itens: [], acumuladoReceber: [], acumuladoPagar: [] })

  render(<ClientPrevistoRealizadoPage />)

  await screen.findByText(/Nenhuma conta fixa recorrente/)
  expect(document.querySelector('.mv-bullet-list')).not.toBeInTheDocument()
})
