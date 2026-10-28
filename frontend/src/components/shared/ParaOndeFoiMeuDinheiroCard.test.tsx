import { render, screen } from '@testing-library/react'
import ParaOndeFoiMeuDinheiroCard from './ParaOndeFoiMeuDinheiroCard'

test('mostra o fluxo Entrou → Gastou → Guardou → Ficou', () => {
  render(<ParaOndeFoiMeuDinheiroCard dados={{ entrou: 10000, gastou: 2000, guardou: 1300, ficou: 6700, taxaPoupancaPercentual: 13, topCategoriasGasto: [] }} />)

  expect(screen.getByText('R$ 10.000,00')).toBeInTheDocument()
  expect(screen.getByText('R$ 2.000,00')).toBeInTheDocument()
  expect(screen.getByText('R$ 1.300,00')).toBeInTheDocument()
  expect(screen.getByText('R$ 6.700,00')).toBeInTheDocument()
  expect(screen.getByText(/guardou R\$ 1\.300,00 este mês/)).toBeInTheDocument()
  expect(screen.getByText('13,0%')).toBeInTheDocument()
  expect(screen.getByText('Guardou')).toBeInTheDocument()
})

// Money Vision: "taxa de poupança negativa com lucro positivo" — um resgate da reserva nunca
// mexe na taxa de poupança (que é sobre entrou-gastou agora), só aparece na frase/fluxo abaixo.
test('resgate da reserva mostra "Você resgatou" e vira "Resgatou" no fluxo, mesmo com taxa positiva', () => {
  render(<ParaOndeFoiMeuDinheiroCard dados={{ entrou: 15895.54, gastou: 13495.10, guardou: -1325.82, ficou: 3726.26, taxaPoupancaPercentual: 15.1, topCategoriasGasto: [] }} />)

  expect(screen.getByText(/Você resgatou R\$ 1\.325,82 da reserva/)).toBeInTheDocument()
  // Rótulo do fluxo vira "Resgatou" (não "Guardou -R$1.325,82") quando negativo.
  expect(screen.getByText('Resgatou')).toBeInTheDocument()
  expect(screen.queryByText('Guardou')).not.toBeInTheDocument()
  expect(screen.getByText('15,1%')).toBeInTheDocument()
  expect(screen.queryByText(/você usou a reserva|você gastou mais do que ganhou/)).not.toBeInTheDocument()
})

test('taxa negativa (prejuízo real no mês) mostra aviso "você gastou mais do que ganhou"', () => {
  render(<ParaOndeFoiMeuDinheiroCard dados={{ entrou: 4000, gastou: 5000, guardou: 0, ficou: -1000, taxaPoupancaPercentual: -25, topCategoriasGasto: [] }} />)

  expect(screen.getByText(/você gastou mais do que ganhou/)).toBeInTheDocument()
  expect(screen.getByText('-25,0%')).toBeInTheDocument()
})

test('sem receita no período, não mostra a taxa de poupança', () => {
  render(<ParaOndeFoiMeuDinheiroCard dados={{ entrou: 0, gastou: 0, guardou: 0, ficou: 0, taxaPoupancaPercentual: null, topCategoriasGasto: [] }} />)

  expect(screen.queryByText(/Taxa de poupança/)).not.toBeInTheDocument()
})
