import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import ClientFinanciamentosPage from './ClientFinanciamentosPage'
import * as AuthContextModule from '../../contexts/AuthContext'
import * as financiamentosApi from '../../api/financiamentos'
import * as contasBancariasApi from '../../api/contasBancarias'
import * as faturasCartaoApi from '../../api/faturasCartao'
import type { ContaBancaria } from '../../types'

vi.mock('../../contexts/AuthContext', async (importOriginal) => {
  const actual = await importOriginal<typeof AuthContextModule>()
  return { ...actual, useAuth: vi.fn() }
})
vi.mock('../../api/financiamentos')
vi.mock('../../api/contasBancarias')
vi.mock('../../api/faturasCartao')

const mockUser = { usuarioId: 'cliente-1', nomeUsuario: 'cli1', perfil: 'cliente' as const, nomeCompleto: 'C', nomeEstabelecimento: '', token: 'tok' }

function conta(overrides: Partial<ContaBancaria>): ContaBancaria {
  return {
    id: 'c1', clienteId: 'cliente-1', nome: 'Conta', tipo: 'ContaCorrente',
    saldoInicial: 0, saldoAtual: 0, entradasMes: 0, saidasMes: 0, pendentesCategorizacao: 0,
    ativa: true, dataCriacao: '2026-01-01',
    ...overrides,
  }
}

function renderPagina() {
  return render(<MemoryRouter><ClientFinanciamentosPage /></MemoryRouter>)
}

beforeEach(() => {
  vi.clearAllMocks()
  vi.mocked(AuthContextModule.useAuth).mockReturnValue({ user: mockUser, login: vi.fn(), logout: vi.fn() } as never)
  vi.mocked(financiamentosApi.listarFinanciamentos).mockResolvedValue([])
})

// Money Vision: "parcelamentos que já existem não aparecem" (item 5) — "Procurar parcelamentos".
test('procurar parcelamentos varre todas as contas de cartão e mostra o resumo encontrado', async () => {
  vi.mocked(contasBancariasApi.listarContasBancarias).mockResolvedValue([
    conta({ id: 'cartao-1', nome: 'Nubank Cartão', tipo: 'CartaoCredito' }),
  ])
  vi.mocked(faturasCartaoApi.procurarParcelamentos).mockResolvedValue({
    parcelamentosEncontrados: 3, valorRestanteTotal: 1234.56, dataFinal: '2027-06-20', parcelamentos: [],
  })
  const user = userEvent.setup()
  renderPagina()

  await user.click(await screen.findByRole('button', { name: /Procurar parcelamentos/ }))

  expect(faturasCartaoApi.procurarParcelamentos).toHaveBeenCalledWith('cartao-1')
  expect(await screen.findByText(/Encontrei 3 parcelamento\(s\)/)).toBeInTheDocument()
  expect(screen.getByText(/R\$ 1\.234,56/)).toBeInTheDocument()
  expect(screen.getByText(/jun\/2027/)).toBeInTheDocument()
})

test('procurar parcelamentos sem conta de cartão avisa sem chamar a API', async () => {
  vi.mocked(contasBancariasApi.listarContasBancarias).mockResolvedValue([
    conta({ id: 'corrente-1', nome: 'Nubank', tipo: 'ContaCorrente' }),
  ])
  const user = userEvent.setup()
  renderPagina()

  await user.click(await screen.findByRole('button', { name: /Procurar parcelamentos/ }))

  expect(faturasCartaoApi.procurarParcelamentos).not.toHaveBeenCalled()
  expect(await screen.findByText('Nenhuma conta de cartão de crédito cadastrada.')).toBeInTheDocument()
})

test('procurar parcelamentos sem nenhum encontrado avisa claramente', async () => {
  vi.mocked(contasBancariasApi.listarContasBancarias).mockResolvedValue([
    conta({ id: 'cartao-1', nome: 'Nubank Cartão', tipo: 'CartaoCredito' }),
  ])
  vi.mocked(faturasCartaoApi.procurarParcelamentos).mockResolvedValue({
    parcelamentosEncontrados: 0, valorRestanteTotal: 0, dataFinal: null, parcelamentos: [],
  })
  const user = userEvent.setup()
  renderPagina()

  await user.click(await screen.findByRole('button', { name: /Procurar parcelamentos/ }))

  expect(await screen.findByText('Nenhum parcelamento novo encontrado.')).toBeInTheDocument()
})
