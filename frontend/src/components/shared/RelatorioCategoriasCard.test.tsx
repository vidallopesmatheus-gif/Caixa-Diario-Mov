import { render, screen } from '@testing-library/react'
import RelatorioCategoriasCard from './RelatorioCategoriasCard'
import type { Registro } from '../../types'

const hoje = new Date().toISOString().slice(0, 10)

const registroBase: Registro = {
  id: 'r1', clienteId: 'c1', data: hoje,
  saldoInicio: 0,
  entradas: [],
  saidas: [
    { descricao: 'Aluguel', valor: 1200, categoria: 'Administrativas', subcategoria: '' },
    { descricao: 'Salário', valor: 3000, categoria: 'Pessoas', subcategoria: '' },
  ],
  contasAReceber: [], contasAPagar: [],
  saldoConfirmado: 0, saldoCalculado: 0, criadoEm: '',
}

test('exibe todas as 7 categorias', () => {
  render(<RelatorioCategoriasCard registros={[registroBase]} />)
  expect(screen.getByText('Administrativas')).toBeInTheDocument()
  expect(screen.getByText('Pessoas')).toBeInTheDocument()
  expect(screen.getByText('Custos Diretos')).toBeInTheDocument()
  expect(screen.getByText('Marketing')).toBeInTheDocument()
  expect(screen.getByText('Impostos')).toBeInTheDocument()
  expect(screen.getByText('Financeiras')).toBeInTheDocument()
  expect(screen.getByText('Investimentos')).toBeInTheDocument()
})

test('exibe label "Total"', () => {
  render(<RelatorioCategoriasCard registros={[registroBase]} />)
  expect(screen.getByText('Total')).toBeInTheDocument()
})

test('exibe inputs de período', () => {
  render(<RelatorioCategoriasCard registros={[registroBase]} />)
  const dateInputs = document.querySelectorAll('input[type="date"]')
  expect(dateInputs.length).toBe(2)
})

test('não exibe registros fora do período', () => {
  const registroAntigo: Registro = { ...registroBase, data: '2020-01-15' }
  render(<RelatorioCategoriasCard registros={[registroAntigo]} />)
  expect(screen.getByText('Total')).toBeInTheDocument()
})
