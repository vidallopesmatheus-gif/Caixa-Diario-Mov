import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import ClassificarTransferenciaModal, { type LancamentoParaTransferencia } from './ClassificarTransferenciaModal'
import * as transferenciasApi from '../../api/transferencias'
import type { ContaBancaria } from '../../types'

vi.mock('../../api/transferencias')

const envio: LancamentoParaTransferencia = { id: 'n-400', data: '2026-10-14', descricao: 'Transferência enviada pelo Pix - Matheus', valor: 400, tipo: 'Saida' }
const contas = [
  { id: 'nubank', nome: 'Nubank', ativa: true },
  { id: 'c6', nome: 'C6 Bank', ativa: true },
] as ContaBancaria[]

function renderModal(onNaoETransferencia = vi.fn()) {
  const onConvertido = vi.fn()
  render(<ClassificarTransferenciaModal contaId="nubank" lancamento={envio} contas={contas} onClose={vi.fn()} onConvertido={onConvertido} onNaoETransferencia={onNaoETransferencia} />)
  return { onConvertido, onNaoETransferencia }
}

beforeEach(() => {
  vi.clearAllMocks()
  vi.mocked(transferenciasApi.converterLancamentoEmTransferencia).mockResolvedValue({} as transferenciasApi.Transferencia)
})

describe('ClassificarTransferenciaModal', () => {
  it('should pair with the real entry of the other account when there is exactly one', async () => {
    vi.mocked(transferenciasApi.listarCandidatosContrapartida).mockResolvedValue({
      contaContrapartidaNome: 'C6 Bank', extratoCobreData: true,
      candidatos: [{ id: 'c6-400', data: '2026-10-15', descricao: 'Pix recebido de Matheus', valor: 400 }],
    })
    const { onConvertido } = renderModal()

    await userEvent.selectOptions(screen.getByLabelText('Conta de destino'), 'c6')
    expect(await screen.findByText(/Vai parear com "Pix recebido de Matheus"/)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Parear' }))

    expect(transferenciasApi.converterLancamentoEmTransferencia).toHaveBeenCalledWith(expect.objectContaining({
      contaContrapartidaId: 'c6', lancamentoContrapartidaId: 'c6-400', dataContrapartida: '2026-10-15', criarMesmoAssim: false,
    }))
    expect(onConvertido).toHaveBeenCalled()
  })

  it('should warn and not create anything when the other account statement covers the date without the entry', async () => {
    vi.mocked(transferenciasApi.listarCandidatosContrapartida).mockResolvedValue({ contaContrapartidaNome: 'C6 Bank', extratoCobreData: true, candidatos: [] })
    renderModal()

    await userEvent.selectOptions(screen.getByLabelText('Conta de destino'), 'c6')

    expect(await screen.findByRole('alert')).toHaveTextContent('O extrato do C6 Bank não tem esse recebimento em 14/10/2026. Tem certeza que foi para o C6 Bank?')
    expect(screen.queryByRole('button', { name: 'Parear' })).not.toBeInTheDocument()
    expect(transferenciasApi.converterLancamentoEmTransferencia).not.toHaveBeenCalled()
  })

  it('should create the counterpart only when the user confirms "criar mesmo assim"', async () => {
    vi.mocked(transferenciasApi.listarCandidatosContrapartida).mockResolvedValue({ contaContrapartidaNome: 'C6 Bank', extratoCobreData: true, candidatos: [] })
    renderModal()

    await userEvent.selectOptions(screen.getByLabelText('Conta de destino'), 'c6')
    await userEvent.click(await screen.findByRole('button', { name: 'Criar mesmo assim' }))

    expect(transferenciasApi.converterLancamentoEmTransferencia).toHaveBeenCalledWith(expect.objectContaining({ criarMesmoAssim: true, lancamentoContrapartidaId: undefined }))
  })

  it('should hand the entry back to categorization when the money went to a third party', async () => {
    vi.mocked(transferenciasApi.listarCandidatosContrapartida).mockResolvedValue({ contaContrapartidaNome: 'C6 Bank', extratoCobreData: true, candidatos: [] })
    const { onNaoETransferencia } = renderModal()

    await userEvent.selectOptions(screen.getByLabelText('Conta de destino'), 'c6')
    await userEvent.click(await screen.findByRole('button', { name: 'É saída para terceiro' }))

    expect(onNaoETransferencia).toHaveBeenCalled()
    expect(transferenciasApi.converterLancamentoEmTransferencia).not.toHaveBeenCalled()
  })

  it('should ask which entry is the other end when there are several candidates', async () => {
    vi.mocked(transferenciasApi.listarCandidatosContrapartida).mockResolvedValue({
      contaContrapartidaNome: 'C6 Bank', extratoCobreData: true,
      candidatos: [
        { id: 'a', data: '2026-10-14', descricao: 'Pix recebido de Matheus', valor: 400 },
        { id: 'b', data: '2026-10-15', descricao: 'Pix recebido de Matheus', valor: 400 },
      ],
    })
    renderModal()

    await userEvent.selectOptions(screen.getByLabelText('Conta de destino'), 'c6')
    expect(await screen.findByRole('button', { name: 'Parear' })).toBeDisabled()
    await userEvent.click(screen.getAllByRole('radio')[1])
    await userEvent.click(screen.getByRole('button', { name: 'Parear' }))

    expect(transferenciasApi.converterLancamentoEmTransferencia).toHaveBeenCalledWith(expect.objectContaining({ lancamentoContrapartidaId: 'b' }))
  })

  it('should explain the provisional counterpart when the other account has no statement for the date', async () => {
    vi.mocked(transferenciasApi.listarCandidatosContrapartida).mockResolvedValue({ contaContrapartidaNome: 'C6 Bank', extratoCobreData: false, candidatos: [] })
    renderModal()

    await userEvent.selectOptions(screen.getByLabelText('Conta de destino'), 'c6')

    expect(await screen.findByText(/Fica uma contrapartida provisória/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Confirmar' })).toBeEnabled()
  })
})
