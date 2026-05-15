import { render, screen } from '@testing-library/react'
import StatCard from './StatCard'

test('renderiza label e valor', () => {
  render(<StatCard label="💰 Saldo" value="R$ 1.234,50" />)
  expect(screen.getByText('💰 Saldo')).toBeInTheDocument()
  expect(screen.getByText('R$ 1.234,50')).toBeInTheDocument()
})

test('aplica className extra no val', () => {
  render(<StatCard label="x" value="y" className="val-green" />)
  expect(screen.getByText('y').className).toContain('val-green')
})
