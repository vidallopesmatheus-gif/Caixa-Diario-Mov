import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import ClientBancoPage from './ClientBancoPage'
import * as AuthContextModule from '../../contexts/AuthContext'
import * as contasBancariasApi from '../../api/contasBancarias'
import * as transferenciasApi from '../../api/transferencias'
import * as metricasApi from '../../api/metricas'
import type { ContaBancaria } from '../../types'

vi.mock('../../contexts/AuthContext', async (importOriginal) => {
  const actual = await importOriginal<typeof AuthContextModule>()
  return { ...actual, useAuth: vi.fn() }
})
vi.mock('../../api/contasBancarias')
vi.mock('../../api/transferencias')
vi.mock('../../api/metricas')

const mockUser = { usuarioId: 'cliente-1', nomeUsuario: 'cli1', perfil: 'cliente' as const, nomeCompleto: 'C', nomeEstabelecimento: '', token: 'tok' }

function conta(overrides: Partial<ContaBancaria>): ContaBancaria {
  return {
    id: 'c1', clienteId: 'cliente-1', nome: 'Conta', tipo: 'ContaCorrente',
    saldoInicial: 0, saldoAtual: 0, entradasMes: 0, saidasMes: 0, pendentesCategorizacao: 0,
    ativa: true, dataCriacao: '2026-01-01',
    ...overrides,
  }
}

beforeEach(() => {
  vi.clearAllMocks()
  vi.mocked(AuthContextModule.useAuth).mockReturnValue({ user: mockUser, login: vi.fn(), logout: vi.fn() } as never)
  vi.mocked(transferenciasApi.listarSugestoesTransferenciaSemPar).mockResolvedValue([])
  vi.mocked(metricasApi.obterParaOndeFoiMeuDinheiro).mockResolvedValue({ entrou: 0, gastou: 0, guardou: 0, ficou: 0, taxaPoupancaPercentual: null, topCategoriasGasto: [] })
})

test('Bloco 5: Disponível, Investido e Patrimônio aparecem separados — investimento não soma no disponível', async () => {
  vi.mocked(contasBancariasApi.listarContasBancarias).mockResolvedValue([
    conta({ id: 'corrente', nome: 'Nubank', tipo: 'ContaCorrente', saldoAtual: 8939.80 }),
    conta({ id: 'invest', nome: 'Nubank Investimento', tipo: 'Investimento', saldoAtual: 4046.68 }),
  ])

  render(<MemoryRouter><ClientBancoPage /></MemoryRouter>)

  await screen.findByText('Disponível para usar')
  const resumo = document.querySelector('.cb-resumo') as HTMLElement
  expect(within(resumo).getByText('R$ 8.939,80')).toBeInTheDocument() // Disponível para usar
  expect(within(resumo).getByText('R$ 4.046,68')).toBeInTheDocument() // Investido / Reserva
  expect(within(resumo).getByText('R$ 12.986,48')).toBeInTheDocument() // Patrimônio total (soma)
})

// Item 5 (Money Vision): entradasMes/saidasMes excluem Transferencia por design — pra conta de
// Investimento (que só recebe aportes/resgates via Transferencia), o card de "Este mês" tem que
// usar aporteMesAtual/resgateMesAtual, senão mostra sempre R$ 0,00.
test('card de conta de Investimento mostra aporte/resgate do mês, não entradasMes/saidasMes', async () => {
  vi.mocked(contasBancariasApi.listarContasBancarias).mockResolvedValue([
    conta({
      id: 'invest', nome: 'Nubank Investimento', tipo: 'Investimento', saldoAtual: 4046.68,
      entradasMes: 0, saidasMes: 0, aporteMesAtual: 500, resgateMesAtual: 120,
    }),
  ])

  render(<MemoryRouter><ClientBancoPage /></MemoryRouter>)

  const linha = await screen.findByText('Nubank Investimento')
  const item = linha.closest('.cb-conta-item') as HTMLElement
  expect(within(item).getByText('+R$ 500,00')).toBeInTheDocument()
  expect(within(item).getByText('-R$ 120,00')).toBeInTheDocument()
})

// Money Vision: "card mostra R$ 0,00, mas as faturas dizem outra coisa" (item 4) — o card de
// cartão tem que usar faturasEmAberto (soma das Faturas reais não pagas), não saldoDevedor (ledger
// inteiro, pode zerar com um pagamento nunca vinculado a nenhuma fatura), e mostrar compras/
// pagamentos do mês em vez de entradasMes/saidasMes (que excluem ambos por serem "não operacionais").
test('card de cartão de crédito mostra faturasEmAberto e compras/pagamentos do mês', async () => {
  vi.mocked(contasBancariasApi.listarContasBancarias).mockResolvedValue([
    conta({
      id: 'cartao', nome: 'Cartões', tipo: 'CartaoCredito', saldoAtual: 349.17,
      saldoDevedor: 0, faturasEmAberto: 2141.52, comprasMes: 1800, pagamentosMes: 2490.69,
    }),
  ])

  render(<MemoryRouter><ClientBancoPage /></MemoryRouter>)

  const linha = await screen.findByText('Cartões')
  const item = linha.closest('.cb-conta-item') as HTMLElement
  expect(within(item).getByText('R$ 2.141,52')).toBeInTheDocument() // faturasEmAberto, não os 0 de saldoDevedor
  expect(within(item).getByText('-R$ 1.800,00')).toBeInTheDocument()
  expect(within(item).getByText('+R$ 2.490,69')).toBeInTheDocument()
})

test('sem conta de investimento, o card "Investido / Reserva" não aparece', async () => {
  vi.mocked(contasBancariasApi.listarContasBancarias).mockResolvedValue([
    conta({ id: 'corrente', nome: 'Nubank', tipo: 'ContaCorrente', saldoAtual: 1000 }),
  ])

  render(<MemoryRouter><ClientBancoPage /></MemoryRouter>)

  await screen.findByText('Disponível para usar')
  expect(screen.queryByText('Investido / Reserva')).not.toBeInTheDocument()
})
