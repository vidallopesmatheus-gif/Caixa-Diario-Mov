// frontend/src/pages/client/ClientHistoricoPage.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import ClientHistoricoPage from './ClientHistoricoPage'
import * as AuthContextModule from '../../contexts/AuthContext'
import * as useRegistrosHook from '../../hooks/useRegistros'
import type { Registro } from '../../types'

vi.mock('../../contexts/AuthContext', async (importOriginal) => {
  const actual = await importOriginal<typeof AuthContextModule>()
  return { ...actual, useAuth: vi.fn() }
})
vi.mock('../../hooks/useRegistros')

const mockUser = { usuarioId: 'u1', nomeUsuario: 'cli1', perfil: 'cliente' as const, nomeCompleto: 'C', nomeEstabelecimento: '', token: 'tok' }

const mesAtual = new Date().toISOString().slice(0, 7)
const mockRegistros: Registro[] = [
  {
    id: 'r1', clienteId: 'u1', data: `${mesAtual}-10`,
    saldoInicio: 100, entrada: 500,
    saidas: [{ descricao: 'Aluguel', valor: 200 }],
    contasAReceber: [], contasAPagar: [],
    saldoConfirmado: 400, saldoCalculado: 400, criadoEm: '',
  },
]

function mockHooks(overrides: Partial<ReturnType<typeof useRegistrosHook.useRegistros>> = {}) {
  vi.mocked(AuthContextModule.useAuth).mockReturnValue({ user: mockUser, login: vi.fn(), logout: vi.fn() })
  vi.mocked(useRegistrosHook.useRegistros).mockReturnValue({
    registros: mockRegistros,
    loading: false,
    erro: '',
    salvar: vi.fn(),
    excluir: vi.fn().mockResolvedValue(undefined),
    buscarPorData: vi.fn(),
    recarregar: vi.fn(),
    ...overrides,
  } as ReturnType<typeof useRegistrosHook.useRegistros>)
}

test('exibe loading enquanto carrega', () => {
  mockHooks({ loading: true, registros: [] })
  render(<ClientHistoricoPage />)
  expect(screen.getByText(/Carregando/)).toBeInTheDocument()
})

test('renderiza StatCards com totais do mês', () => {
  mockHooks()
  render(<ClientHistoricoPage />)
  expect(screen.getByText(/Total entradas/)).toBeInTheDocument()
  expect(screen.getByText(/Total saídas/)).toBeInTheDocument()
})

test('renderiza item de histórico com data e saldo', () => {
  mockHooks()
  render(<ClientHistoricoPage />)
  expect(screen.getAllByText(/R\$/).length).toBeGreaterThan(0)
})

test('expande detalhes ao clicar no item de histórico', async () => {
  mockHooks()
  render(<ClientHistoricoPage />)
  const items = screen.getAllByText(/Entrada:/)
  fireEvent.click(items[0].closest('div')!.parentElement!)
  await waitFor(() =>
    expect(screen.getByText('Aluguel')).toBeInTheDocument()
  )
})

test('abre modal de exclusão ao clicar em Excluir registro', async () => {
  mockHooks()
  render(<ClientHistoricoPage />)
  const items = screen.getAllByText(/Entrada:/)
  fireEvent.click(items[0].closest('div')!.parentElement!)
  await waitFor(() => screen.getByText(/Excluir registro/))
  fireEvent.click(screen.getByText(/Excluir registro/))
  expect(screen.getByText(/Motivo/)).toBeInTheDocument()
})

test('chama excluir com motivo ao confirmar', async () => {
  const excluir = vi.fn().mockResolvedValue(undefined)
  mockHooks({ excluir })
  render(<ClientHistoricoPage />)
  const items = screen.getAllByText(/Entrada:/)
  fireEvent.click(items[0].closest('div')!.parentElement!)
  await waitFor(() => screen.getByText(/Excluir registro/))
  fireEvent.click(screen.getByText(/Excluir registro/))
  fireEvent.change(screen.getByPlaceholderText(/Informe o motivo/), { target: { value: 'teste' } })
  fireEvent.click(screen.getByText('Excluir'))
  await waitFor(() => expect(excluir).toHaveBeenCalledWith(`${mesAtual}-10`, 'teste'))
})
