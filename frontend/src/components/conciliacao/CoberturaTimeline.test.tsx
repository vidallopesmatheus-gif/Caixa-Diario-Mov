import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import CoberturaTimeline from './CoberturaTimeline'
import type { CoberturaExtrato } from '../../types'

// "Hoje" fixo: a régua vai até o mês atual, e o teste não pode depender da data real.
beforeEach(() => { vi.useFakeTimers({ toFake: ['Date'] }); vi.setSystemTime(new Date('2026-11-08T15:00:00Z')) })
afterEach(() => { vi.useRealTimers() })

const cobertura: CoberturaExtrato = {
  diasComExtrato: ['2026-11-01', '2026-11-02', '2026-11-06', '2026-11-07'],
  pontos: [{ data: '2026-11-07', saldoBanco: 100, saldoApp: 100, diferenca: 0, conciliado: true, origem: 'Ofx' }],
  saldosPorDia: [{ data: '2026-11-01', saldoFinal: 100 }],
  status: { saldoApp: 100 },
}

describe('CoberturaTimeline', () => {
  it('should filter the whole gap when the user clicks a day without extrato', async () => {
    const onSelecionarPeriodo = vi.fn()
    render(<CoberturaTimeline cobertura={cobertura} onSelecionarPeriodo={onSelecionarPeriodo} />)

    await userEvent.click(screen.getByText('Cobertura do extrato'))
    await userEvent.click(screen.getByRole('button', { name: 'Sem extrato em 04/11/2026 — filtrar o período' }))

    expect(onSelecionarPeriodo).toHaveBeenCalledWith('2026-11-03', '2026-11-05')
  })

  it('should mark the conciliated day with a check', async () => {
    render(<CoberturaTimeline cobertura={cobertura} onSelecionarPeriodo={vi.fn()} />)

    await userEvent.click(screen.getByText('Cobertura do extrato'))

    expect(screen.getByTitle(/07\/11\/2026 — com extrato · banco/)).toHaveTextContent('✅')
  })
})
