import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import ReabrirPeriodoModal, { SeloPeriodoConferido } from './PeriodoConferido'
import * as AuthContextModule from '../../contexts/AuthContext'
import * as contasApi from '../../api/contasBancarias'
import { ApiError } from '../../api/client'
import { diaTravado, erroPeriodoConferido } from '../../utils/periodoConferido'

vi.mock('../../contexts/AuthContext', async (importOriginal) => {
  const actual = await importOriginal<typeof AuthContextModule>()
  return { ...actual, useAuth: vi.fn() }
})
vi.mock('../../api/contasBancarias')

const nubank = { id: 'nubank', nome: 'Nubank', travadoAte: '2026-11-07', travaSaldoBanco: 86.4, travadoPorConsultor: false }

function comoPerfil(perfil: 'cliente' | 'admin') {
  vi.mocked(AuthContextModule.useAuth).mockReturnValue({
    user: { usuarioId: 'u1', nomeUsuario: 'u', perfil, nomeCompleto: 'U', nomeEstabelecimento: '', token: 't' },
    login: vi.fn(), logout: vi.fn(),
  } as never)
}

beforeEach(() => {
  vi.clearAllMocks()
  comoPerfil('cliente')
  vi.mocked(contasApi.listarConferencias).mockResolvedValue([])
  vi.mocked(contasApi.reabrirPeriodo).mockResolvedValue({ travadoAte: '2026-11-02' })
})

describe('SeloPeriodoConferido', () => {
  it('should show the checked period and the bank balance', () => {
    render(<SeloPeriodoConferido conta={nubank} onReabrir={vi.fn()} />)

    expect(screen.getByRole('status')).toHaveTextContent('🔒 Conferido até 07/11/2026 · Banco R$ 86,40 ✅')
    expect(screen.getByRole('button', { name: 'Reabrir período' })).toBeInTheDocument()
  })

  it('should ask the consultant to reopen when the client sees a period checked by the consultant', () => {
    render(<SeloPeriodoConferido conta={{ ...nubank, travadoPorConsultor: true }} onReabrir={vi.fn()} />)

    expect(screen.getByText('Período conferido pelo consultor — peça para reabrir')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Reabrir período' })).not.toBeInTheDocument()
  })
})

describe('ReabrirPeriodoModal', () => {
  it('should reopen from the chosen date with the reason when the user confirms', async () => {
    const onReaberto = vi.fn()
    render(<ReabrirPeriodoModal conta={nubank} dataSugerida="2026-11-03" onClose={vi.fn()} onReaberto={onReaberto} />)

    await userEvent.type(screen.getByLabelText('Motivo (opcional)'), 'banco lançou estorno com data de 03/11')
    await userEvent.click(screen.getByRole('button', { name: 'Reabrir' }))

    expect(contasApi.reabrirPeriodo).toHaveBeenCalledWith('nubank', '2026-11-03', 'banco lançou estorno com data de 03/11')
    expect(onReaberto).toHaveBeenCalled()
    expect(screen.getByText('Depois de corrigir, confira o saldo de novo para travar.')).toBeInTheDocument()
  })

  it('should reopen everything when the user picks "Reabrir tudo"', async () => {
    render(<ReabrirPeriodoModal conta={nubank} onClose={vi.fn()} onReaberto={vi.fn()} />)

    await userEvent.click(screen.getByLabelText('Reabrir tudo'))
    await userEvent.click(screen.getByRole('button', { name: 'Reabrir' }))

    expect(contasApi.reabrirPeriodo).toHaveBeenCalledWith('nubank', null, '')
  })

  it('should list who checked and who reopened the period', async () => {
    vi.mocked(contasApi.listarConferencias).mockResolvedValue([
      { tipo: 'Reabertura', data: '2026-11-03', quem: 'Cliente', motivo: 'estorno', ocorridoEm: '2026-11-12T10:00:00Z' },
      { tipo: 'Conferencia', data: '2026-11-07', saldoBanco: 86.4, quem: 'Consultor', ocorridoEm: '2026-11-08T10:00:00Z' },
    ])

    render(<ReabrirPeriodoModal conta={nubank} onClose={vi.fn()} onReaberto={vi.fn()} />)

    expect(await screen.findByText(/reabriu a partir de 03\/11\/2026 — “estorno”/)).toBeInTheDocument()
    expect(screen.getByText(/Consultor · conferiu até 07\/11\/2026/)).toBeInTheDocument()
  })
})

describe('periodoConferido utils', () => {
  it('should treat the checked date itself as locked', () => {
    expect([diaTravado(nubank, '2026-11-07'), diaTravado(nubank, '2026-11-08')]).toEqual([true, false])
  })

  it('should read the 409 PERIODO_CONFERIDO details from the API error', () => {
    const erro = new ApiError('PERIODO_CONFERIDO', 'Período da conta Nubank conferido até 07/11/2026 — reabra o período para alterar.', undefined,
      { contaId: 'nubank', conta: 'Nubank', travadoAte: '2026-11-07', peloConsultor: false })

    expect(erroPeriodoConferido(erro)).toMatchObject({ contaId: 'nubank', travadoAte: '2026-11-07' })
    expect(erroPeriodoConferido(new Error('outro'))).toBeNull()
  })
})
