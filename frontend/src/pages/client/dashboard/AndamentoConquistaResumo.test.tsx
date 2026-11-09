import { render, screen } from '@testing-library/react'
import AndamentoConquistaResumo from './AndamentoConquistaResumo'
import type { AndamentoConquista } from '../../../types'

const macbook: AndamentoConquista = {
  valorPago: 409.16, valorTotal: 4091.6, dataTermino: '2027-07-20', valorMensal: 409.16,
  parcelamentos: [
    { id: 'p-nu', descricao: 'Trocafone', banco: 'Nubank', parcelasPagas: 1, totalParcelas: 10, valorParcela: 199.16, valorTotal: 1991.6, dataUltimaParcela: '2027-07-20' },
    { id: 'p-c6', descricao: 'Trocafone', banco: 'C6', parcelasPagas: 1, totalParcelas: 10, valorParcela: 210, valorTotal: 2100, dataUltimaParcela: '2027-07-20' },
  ],
}

describe('AndamentoConquistaResumo', () => {
  it('should show paid percentage and installment count when the goal has two card installments', () => {
    render(<AndamentoConquistaResumo andamento={macbook} />)

    expect(screen.getByText(/Pago R\$\s409,16 de R\$\s4\.091,60 \(10%\) · 2 parcelamentos/)).toBeInTheDocument()
  })

  it('should show one line per card with the end month when installments are open', () => {
    render(<AndamentoConquistaResumo andamento={macbook} />)

    expect(screen.getByText('Nubank · 1 de 10 · termina jul/2027')).toBeInTheDocument()
    expect(screen.getByText('C6 · 1 de 10 · termina jul/2027')).toBeInTheDocument()
  })

  it('should show the monthly amount freed after the last installment', () => {
    render(<AndamentoConquistaResumo andamento={macbook} />)

    expect(screen.getByText(/A partir de ago\/2027 sobram R\$\s409,16 por mês/)).toBeInTheDocument()
  })
})
