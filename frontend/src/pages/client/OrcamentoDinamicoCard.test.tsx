import { render, screen } from '@testing-library/react'
import OrcamentoDinamicoCard from './OrcamentoDinamicoCard'
import * as orcamentoDinamicoApi from '../../api/orcamentoDinamico'
import type { OrcamentoDinamico } from '../../api/orcamentoDinamico'

vi.mock('../../api/orcamentoDinamico')

function mockDados(overrides: Partial<OrcamentoDinamico>): OrcamentoDinamico {
  return {
    receitaEsperada: 10000,
    compromissosFixos: 2000,
    aporteNecessario: 0,
    saldoLivre: 8000,
    gastoVariavelAtual: 0,
    ultrapassado: false,
    percentualUtilizado: 0,
    ...overrides,
  }
}

// Money Vision: "números que se contradizem" (item 1) — "disponível" tem que ser o que ainda
// sobra (saldoLivre − gasto já feito), nunca o teto do orçamento sozinho.
test('disponível mostra saldo livre menos o gasto variável já feito, não o saldo livre sozinho', async () => {
  vi.mocked(orcamentoDinamicoApi.obterOrcamentoDinamico).mockResolvedValue(
    mockDados({ saldoLivre: 10819.02, gastoVariavelAtual: 13495.10, ultrapassado: true, percentualUtilizado: 124.7 })
  )

  render(<OrcamentoDinamicoCard clienteId="cliente-1" />)

  // disponivel = 10819.02 - 13495.10 = -2676.08 → mostra o valor absoluto com aviso de limite passado.
  expect(await screen.findByText('R$ 2.676,08')).toBeInTheDocument()
  expect(screen.getByText('você passou do limite de gastos variáveis este mês')).toBeInTheDocument()
})

test('dentro do limite mostra o valor que ainda sobra e o texto "disponível"', async () => {
  vi.mocked(orcamentoDinamicoApi.obterOrcamentoDinamico).mockResolvedValue(
    mockDados({ saldoLivre: 8000, gastoVariavelAtual: 3000, percentualUtilizado: 37.5 })
  )

  render(<OrcamentoDinamicoCard clienteId="cliente-1" />)

  expect(await screen.findByText('R$ 5.000,00')).toBeInTheDocument()
  expect(screen.getByText('disponível para gastos variáveis este mês')).toBeInTheDocument()
})

test('sem margem (saldo livre <= 0) mostra o aviso de sem margem, não o de limite passado', async () => {
  vi.mocked(orcamentoDinamicoApi.obterOrcamentoDinamico).mockResolvedValue(
    mockDados({ saldoLivre: 0, gastoVariavelAtual: 500 })
  )

  render(<OrcamentoDinamicoCard clienteId="cliente-1" />)

  expect(await screen.findByText('sem margem — receita não cobre compromissos e aportes')).toBeInTheDocument()
})
