import { render, screen } from '@testing-library/react'
import ParaOndeFoiMeuDinheiroCard from './ParaOndeFoiMeuDinheiroCard'

test('mostra o fluxo Entrou → Gastou → Guardou → Ficou', () => {
  render(<ParaOndeFoiMeuDinheiroCard dados={{ entrou: 10000, gastou: 2000, guardou: 1300, ficou: 6700, taxaPoupancaPercentual: 13 }} />)

  expect(screen.getByText('R$ 10.000,00')).toBeInTheDocument()
  expect(screen.getByText('R$ 2.000,00')).toBeInTheDocument()
  expect(screen.getByText('R$ 1.300,00')).toBeInTheDocument()
  expect(screen.getByText('R$ 6.700,00')).toBeInTheDocument()
  expect(screen.getByText(/guardou R\$ 1\.300,00 este mês/)).toBeInTheDocument()
  expect(screen.getByText('13,0%')).toBeInTheDocument()
  expect(screen.getByText('Guardou')).toBeInTheDocument()
})

test('taxa negativa mostra aviso "você usou a reserva" e vira "Resgatou" no fluxo', () => {
  render(<ParaOndeFoiMeuDinheiroCard dados={{ entrou: 5000, gastou: 4000, guardou: -500, ficou: 1500, taxaPoupancaPercentual: -10 }} />)

  expect(screen.getByText(/você usou a reserva/)).toBeInTheDocument()
  expect(screen.getByText(/Você resgatou R\$ 500,00 da reserva/)).toBeInTheDocument()
  // Item 5 (Money Vision): rótulo do fluxo vira "Resgatou" (não "Guardou -R$500,00") quando negativo.
  expect(screen.getByText('Resgatou')).toBeInTheDocument()
  expect(screen.queryByText('Guardou')).not.toBeInTheDocument()
  expect(screen.getByText('-10,0%')).toBeInTheDocument()
})

test('sem receita no período, não mostra a taxa de poupança', () => {
  render(<ParaOndeFoiMeuDinheiroCard dados={{ entrou: 0, gastou: 0, guardou: 0, ficou: 0, taxaPoupancaPercentual: null }} />)

  expect(screen.queryByText(/Taxa de poupança/)).not.toBeInTheDocument()
})
