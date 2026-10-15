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

// Redesign visual (Money Vision): o gráfico de linha combinado deu lugar a um "bullet chart" por
// categoria (uma linha por categoria, dentro de .mv-bullet-list) — cobertura adaptada a esse novo
// comportamento, mantendo o foco em "os dados certos aparecem", não na estrutura antiga da tela.
test('mostra o bullet chart por categoria quando há dados', async () => {
  mockHooks({
    linhas: [
      {
        tipo: 'Pagar', categoria: 'Aluguel',
        meses: [
          { mes: '2026-09', previsto: 1000, realizado: 1000, variacaoAlta: false, subiuTresMesesSeguidos: false },
          { mes: '2026-10', previsto: 1000, realizado: 1100, variacaoAlta: false, subiuTresMesesSeguidos: false },
        ],
      },
    ],
  })

  render(<ClientPrevistoRealizadoPage />)

  await waitFor(() => expect(screen.getAllByText('Aluguel').length).toBeGreaterThan(0))
  expect(document.querySelector('.mv-bullet-list')).toBeInTheDocument()
})

test('soma previsto e realizado de várias categorias no período, nos KPIs', async () => {
  mockHooks({
    linhas: [
      {
        tipo: 'Pagar', categoria: 'Aluguel',
        meses: [{ mes: '2026-10', previsto: 1000, realizado: 1050, variacaoAlta: false, subiuTresMesesSeguidos: false }],
      },
      {
        tipo: 'Pagar', categoria: 'Energia',
        meses: [{ mes: '2026-10', previsto: 200, realizado: 250, variacaoAlta: false, subiuTresMesesSeguidos: false }],
      },
    ],
  })

  render(<ClientPrevistoRealizadoPage />)

  await waitFor(() => expect(screen.getAllByText('Aluguel').length).toBeGreaterThan(0))
  // Soma do período inteiro (ambas categorias, tipo Pagar): previsto 1000+200, realizado 1050+250.
  expect(screen.getByText('R$ 1.200,00')).toBeInTheDocument()
  expect(screen.getByText('R$ 1.300,00')).toBeInTheDocument()
})

test('sem dados, não mostra o bullet chart', async () => {
  mockHooks({ linhas: [] })

  render(<ClientPrevistoRealizadoPage />)

  await screen.findByText(/Nenhuma conta fixa recorrente/)
  expect(document.querySelector('.mv-bullet-list')).not.toBeInTheDocument()
})
