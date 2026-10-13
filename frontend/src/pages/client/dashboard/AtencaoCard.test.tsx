import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import AtencaoCard from './AtencaoCard'
import * as insightsApi from '../../../api/insights'
import { todayISO, addDays } from '../../../utils/format'
import type { Registro, ContaBancaria } from '../../../types'

vi.mock('../../../api/insights')

function conta(overrides: Partial<ContaBancaria>): ContaBancaria {
  return {
    id: 'c1', clienteId: 'cliente-1', nome: 'Conta', tipo: 'ContaCorrente',
    saldoInicial: 0, saldoAtual: 0, entradasMes: 0, saidasMes: 0, pendentesCategorizacao: 0,
    ativa: true, dataCriacao: '2026-01-01',
    ...overrides,
  }
}

function registro(overrides: Partial<Registro>): Registro {
  return {
    id: 'r1', clienteId: 'cliente-1', data: todayISO(), saldoInicio: 0,
    entradas: [], saidas: [], contasAReceber: [], contasAPagar: [],
    saldoConfirmado: 0, saldoCalculado: 0, criadoEm: '2026-01-01',
    ...overrides,
  }
}

beforeEach(() => {
  vi.mocked(insightsApi.obterInsights).mockResolvedValue([])
})

function renderCard(registros: Registro[], contasBancarias: ContaBancaria[]) {
  return render(
    <MemoryRouter>
      <AtencaoCard clienteId="cliente-1" registros={registros} contasBancarias={contasBancarias} />
    </MemoryRouter>,
  )
}

test('Bloco 5: alerta de resgate quando contas a pagar em 7 dias superam o disponível em conta', async () => {
  const registros = [
    registro({
      contasAPagar: [{ descricao: 'Fornecedor', valor: 5000, pago: false, dataVencimento: addDays(todayISO(), 5) }],
    }),
  ]
  const contas = [
    conta({ tipo: 'ContaCorrente', saldoAtual: 1000 }),
    conta({ id: 'c2', tipo: 'Investimento', saldoAtual: 8000 }),
  ]

  renderCard(registros, contas)

  expect(await screen.findByText(/R\$ 5\.000,00 pra pagar nos próximos 7 dias e R\$ 1\.000,00 disponível/)).toBeInTheDocument()
  expect(screen.getByText(/considere resgatar R\$ 4\.000,00/)).toBeInTheDocument()
})

test('sem investimento disponível, não oferece resgatar (não tem de onde)', async () => {
  const registros = [
    registro({
      contasAPagar: [{ descricao: 'Fornecedor', valor: 5000, pago: false, dataVencimento: addDays(todayISO(), 5) }],
    }),
  ]
  const contas = [conta({ tipo: 'ContaCorrente', saldoAtual: 1000 })]

  renderCard(registros, contas)

  await screen.findByText('🔎 Precisa de atenção')
  expect(screen.queryByText(/considere resgatar/)).not.toBeInTheDocument()
})

test('Bloco 5: alerta de dinheiro parado quando disponível supera o gasto médio mensal', async () => {
  const hoje = todayISO()
  const mesAnterior = (i: number) => {
    const d = new Date(hoje + 'T12:00:00')
    d.setMonth(d.getMonth() - i)
    return d.toISOString().slice(0, 10)
  }
  const registros = [1, 2, 3].map(i => registro({
    id: `r-${i}`,
    data: mesAnterior(i),
    saidas: [{ id: 'x', descricao: 'Aluguel', categoria: 'Aluguel', subcategoria: '', valor: 500, pendenteCategorizacao: false }],
  }))
  const contas = [conta({ tipo: 'ContaCorrente', saldoAtual: 2000 })]

  renderCard(registros, contas)

  expect(await screen.findByText(/R\$ 2\.000,00 parados na conta corrente/)).toBeInTheDocument()
  expect(screen.getByText(/Gasto médio dos últimos 3 meses: R\$ 500,00/)).toBeInTheDocument()
})

test('disponível abaixo do gasto médio, não alerta dinheiro parado', async () => {
  const hoje = todayISO()
  const mesAnterior = (i: number) => {
    const d = new Date(hoje + 'T12:00:00')
    d.setMonth(d.getMonth() - i)
    return d.toISOString().slice(0, 10)
  }
  const registros = [1, 2, 3].map(i => registro({
    id: `r-${i}`,
    data: mesAnterior(i),
    saidas: [{ id: 'x', descricao: 'Aluguel', categoria: 'Aluguel', subcategoria: '', valor: 5000, pendenteCategorizacao: false }],
  }))
  const contas = [conta({ tipo: 'ContaCorrente', saldoAtual: 100 })]

  renderCard(registros, contas)

  await screen.findByText('✅ Nada exigindo atenção nos próximos 30 dias.')
})
