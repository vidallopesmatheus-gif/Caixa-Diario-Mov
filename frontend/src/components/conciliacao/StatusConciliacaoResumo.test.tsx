import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import StatusConciliacaoResumo from './StatusConciliacaoResumo'
import type { PontoConciliacao, StatusConciliacao } from '../../types'

const pontoOk: PontoConciliacao = { data: '2026-11-07', saldoBanco: 2400, saldoApp: 2400, diferenca: 0, conciliado: true, origem: 'Ofx' }

function status(overrides: Partial<StatusConciliacao> = {}): StatusConciliacao {
  return { extratoAte: '2026-11-07', diasSemExtrato: 1, saldoApp: 2400, ultimoPonto: pontoOk, ...overrides }
}

describe('StatusConciliacaoResumo', () => {
  it('should show extrato coverage, app balance and the bank check when the last point matches', () => {
    render(<StatusConciliacaoResumo status={status()} />)

    expect(screen.getByText(/Extrato até/)).toHaveTextContent('Extrato até 07/11/2026')
    expect(screen.getByText(/Banco em 07\/11/)).toHaveTextContent('✅')
    expect(screen.queryByText(/Diferença de/)).not.toBeInTheDocument()
  })

  it('should offer the diagnosis when the last bank point diverges', async () => {
    const ponto = { ...pontoOk, saldoBanco: 1715.94, diferenca: -684.06, conciliado: false }
    const onVerDiagnostico = vi.fn()
    render(<StatusConciliacaoResumo status={status({ ultimoPonto: ponto, divergenteDesde: '2026-10-18' })} onVerDiagnostico={onVerDiagnostico} />)

    expect(screen.getByText(/Diferença de R\$\s684,06 desde 18\/10/)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'ver diagnóstico' }))
    expect(onVerDiagnostico).toHaveBeenCalledWith(ponto)
  })

  it('should warn about missing entries when the last extrato is older than 5 days', () => {
    render(<StatusConciliacaoResumo status={status({ diasSemExtrato: 6, ultimoPonto: undefined })} onImportar={vi.fn()} />)

    expect(screen.getByText(/Faltam lançamentos desde 07\/11/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'importar extrato' })).toBeInTheDocument()
  })

  it('should say there is no extrato when nothing was imported yet', () => {
    render(<StatusConciliacaoResumo status={{ saldoApp: 0 }} compacto />)

    expect(screen.getByText('Nenhum extrato importado ainda')).toBeInTheDocument()
  })
})
