import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import AjustesSaldoLista from './AjustesSaldoLista'
import * as conciliacaoApi from '../../api/conciliacaoSaldo'
import * as importacaoApi from '../../api/importacao'
import type { AjusteSaldo } from '../../types'

vi.mock('../../api/conciliacaoSaldo')
vi.mock('../../api/importacao')

const ajusteMigracao: AjusteSaldo = {
  id: 'aj-1', data: '2026-09-30', valor: -3763.10, confirmado: false,
  descricao: 'Ajuste automático da migração — saldo informado anteriormente em 01/10 era R$ 132,66',
  saldoDoDiaSemAjuste: 3895.76, saldoDoDia: 132.66,
}

beforeEach(() => {
  vi.clearAllMocks()
  vi.mocked(conciliacaoApi.listarAjustesSaldo).mockResolvedValue([ajusteMigracao])
  vi.mocked(conciliacaoApi.confirmarAjusteSaldo).mockResolvedValue()
  vi.mocked(importacaoApi.excluirLancamento).mockResolvedValue({ transferenciaExcluida: false, tituloReaberto: null })
})

describe('AjustesSaldoLista', () => {
  it('should show what each adjustment did to the balance of the day', async () => {
    render(<AjustesSaldoLista contaId="conta-1" onAlterado={vi.fn()} />)

    expect(await screen.findByText(/Ajuste automático da migração/)).toBeInTheDocument()
    expect(screen.getByText(/Saldo do dia: R\$\s3\.895,76 → R\$\s132,66/)).toBeInTheDocument()
  })

  it('should keep the balance and only mark the adjustment when the user confirms it', async () => {
    const onAlterado = vi.fn()
    render(<AjustesSaldoLista contaId="conta-1" onAlterado={onAlterado} />)

    await userEvent.click(await screen.findByRole('button', { name: 'Está certo' }))

    expect(conciliacaoApi.confirmarAjusteSaldo).toHaveBeenCalledWith('conta-1', 'aj-1')
    expect(onAlterado).not.toHaveBeenCalled()
  })

  it('should delete the adjustment and reload the account when the user undoes it', async () => {
    const onAlterado = vi.fn()
    render(<AjustesSaldoLista contaId="conta-1" onAlterado={onAlterado} />)

    await userEvent.click(await screen.findByRole('button', { name: 'Desfazer' }))

    expect(importacaoApi.excluirLancamento).toHaveBeenCalledWith('conta-1', { id: 'aj-1', data: '2026-09-30' })
    expect(onAlterado).toHaveBeenCalled()
  })

  it('should say everything was checked when no adjustment is pending', async () => {
    vi.mocked(conciliacaoApi.listarAjustesSaldo).mockResolvedValue([{ ...ajusteMigracao, confirmado: true }])

    render(<AjustesSaldoLista contaId="conta-1" onAlterado={vi.fn()} />)

    expect(await screen.findByText(/Todos os 1 ajuste\(s\) já foram conferidos/)).toBeInTheDocument()
  })
})
