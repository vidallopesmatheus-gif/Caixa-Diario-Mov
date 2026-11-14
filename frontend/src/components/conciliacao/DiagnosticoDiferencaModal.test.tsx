import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import DiagnosticoDiferencaModal from './DiagnosticoDiferencaModal'
import * as conciliacaoApi from '../../api/conciliacaoSaldo'
import * as importacaoApi from '../../api/importacao'
import * as contasApi from '../../api/contasBancarias'
import type { DiagnosticoDiferenca, LinhaDiagnostico } from '../../types'

vi.mock('../../api/conciliacaoSaldo')
vi.mock('../../api/importacao')
vi.mock('../../api/contasBancarias')

const duplicata: LinhaDiagnostico = { id: 'dup-2', data: '2026-11-03', descricao: 'Pix enviado João', valor: -342.03, efeitoSeCorrigir: 342.03 }

function diagnostico(overrides: Partial<DiagnosticoDiferenca> = {}): DiagnosticoDiferenca {
  return {
    data: '2026-11-07', saldoBanco: 557.97, saldoApp: 215.94, diferenca: 342.03, analisadoDesde: '2026-11-01',
    lacunas: [], sinalSuspeito: [], transferenciasSemPar: [], manuais: [], ajustes: [], contrapartidasSemLancamento: [], contrapartidasDuplicadas: [],
    duplicatas: [{ itens: [{ ...duplicata, id: 'dup-1', efeitoSeCorrigir: 0 }, duplicata], somaExcedente: -342.03 }],
    combinacao: { resumo: 'Excluir esta duplicata zera a diferença', itens: [duplicata] },
    ...overrides,
  }
}

function renderModal(onAlterado = vi.fn(), onClose = vi.fn()) {
  render(<MemoryRouter><DiagnosticoDiferencaModal contaId="conta-1" data="2026-11-07" saldoBanco={557.97} onClose={onClose} onAlterado={onAlterado} /></MemoryRouter>)
  return { onAlterado, onClose }
}

beforeEach(() => {
  vi.clearAllMocks()
  vi.mocked(importacaoApi.excluirLancamento).mockResolvedValue({ transferenciaExcluida: false, tituloReaberto: null })
})

describe('DiagnosticoDiferencaModal', () => {
  it('should highlight the duplicate that explains the difference before any adjustment', async () => {
    vi.mocked(conciliacaoApi.diagnosticarSaldo).mockResolvedValue(diagnostico())
    renderModal()

    expect(await screen.findByText('💡 Excluir esta duplicata zera a diferença')).toBeInTheDocument()
    expect(screen.getByText(/Diferença de/)).toHaveTextContent('R$ 342,03')
    expect(contasApi.ajustarSaldo).not.toHaveBeenCalled()
  })

  it('should delete the line and reload the diagnosis when the user removes a duplicate', async () => {
    vi.mocked(conciliacaoApi.diagnosticarSaldo)
      .mockResolvedValueOnce(diagnostico())
      .mockResolvedValueOnce(diagnostico({ saldoApp: 557.97, diferenca: 0, duplicatas: [], combinacao: undefined }))
    const { onAlterado } = renderModal()

    await userEvent.click((await screen.findAllByRole('button', { name: 'Excluir' }))[0])

    expect(importacaoApi.excluirLancamento).toHaveBeenCalledWith('conta-1', { id: 'dup-2', data: '2026-11-03' })
    expect(onAlterado).toHaveBeenCalled()
    expect(await screen.findByText(/Agora o saldo do app bate com o banco/)).toBeInTheDocument()
  })

  it('should post the remaining difference as an adjustment only when the user asks for it', async () => {
    vi.mocked(conciliacaoApi.diagnosticarSaldo).mockResolvedValue(diagnostico({ duplicatas: [], combinacao: undefined }))
    vi.mocked(contasApi.ajustarSaldo).mockResolvedValue({} as never)
    const { onAlterado, onClose } = renderModal()

    await userEvent.click(await screen.findByRole('button', { name: /Lançar ajuste da diferença restante/ }))

    expect(contasApi.ajustarSaldo).toHaveBeenCalledWith('conta-1', { data: '2026-11-07', saldoReal: 557.97 })
    expect(onAlterado).toHaveBeenCalled()
    expect(onClose).toHaveBeenCalled()
  })

  it('should not offer deleting a suspicious-sign line when the fix is inverting it', async () => {
    const sinal: LinhaDiagnostico = { id: 's1', data: '2026-11-03', descricao: 'Pix recebido Maria', valor: -200, efeitoSeCorrigir: 400 }
    vi.mocked(conciliacaoApi.diagnosticarSaldo).mockResolvedValue(diagnostico({ duplicatas: [], combinacao: undefined, sinalSuspeito: [sinal] }))
    renderModal()

    expect(await screen.findByText(/corrija no extrato/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Excluir' })).not.toBeInTheDocument()
  })

  it('should list the balance adjustments as a cause and let the user undo one', async () => {
    const ajuste: LinhaDiagnostico = { id: 'aj-1', data: '2026-11-07', descricao: 'Ajuste de saldo', valor: 11809.52, efeitoSeCorrigir: -11809.52 }
    vi.mocked(conciliacaoApi.diagnosticarSaldo).mockResolvedValue(diagnostico({ duplicatas: [], combinacao: undefined, ajustes: [ajuste] }))
    renderModal()

    await userEvent.click(await screen.findByRole('button', { name: 'Desfazer ajuste' }))

    expect(screen.getByText('Ajustes de saldo no período')).toBeInTheDocument()
    expect(importacaoApi.excluirLancamento).toHaveBeenCalledWith('conta-1', { id: 'aj-1', data: '2026-11-07' })
  })
})

describe('DiagnosticoDiferencaModal — contrapartidas de transferência', () => {
  it('should send the user to the transfer review instead of deleting both ends when a counterpart has no bank entry', async () => {
    const criada: LinhaDiagnostico = { id: 'c6-400', data: '2026-10-14', descricao: '↔ Transferência de Nubank', valor: 400, efeitoSeCorrigir: -400 }
    vi.mocked(conciliacaoApi.diagnosticarSaldo).mockResolvedValue(diagnostico({ duplicatas: [], combinacao: undefined, contrapartidasSemLancamento: [criada] }))
    renderModal()

    expect(await screen.findByText('Transferências que o extrato não tem')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'revisar em Transferências' })).toHaveAttribute('href', '/banco/transferencias-sem-par')
    expect(screen.queryByRole('button', { name: 'Excluir' })).not.toBeInTheDocument()
  })
})
