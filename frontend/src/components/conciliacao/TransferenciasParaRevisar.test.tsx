import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import TransferenciasParaRevisar from './TransferenciasParaRevisar'
import * as transferenciasApi from '../../api/transferencias'

vi.mock('../../api/transferencias')

const duplicada: transferenciasApi.ItemCorrecaoTransferencia = {
  transferenciaId: 't-2004', tipo: 'DuplicadaComReal', acao: 'Trocar a contrapartida criada pelo lançamento real do extrato',
  contaNome: 'Nubank', data: '2026-04-20', valor: -290, descricaoCriada: 'Pix recebido de Matheus Vidal Lopes', descricaoReal: 'Transferência enviada pelo Pix - MATHEUS',
}
const semExtrato: transferenciasApi.TransferenciaParaRevisar = {
  transferenciaId: 't-1410', data: '2026-10-14', valor: 400, contaCriadaNome: 'C6 Bank', contaOutraNome: 'Nubank',
  descricaoOriginal: 'Transferência enviada pelo Pix - Matheus',
}

beforeEach(() => {
  vi.clearAllMocks()
  vi.mocked(transferenciasApi.listarTransferenciasParaRevisar).mockResolvedValue({ corrigiveis: [duplicada], paraDecidir: [semExtrato] })
  vi.mocked(transferenciasApi.corrigirContrapartidasTransferencia).mockResolvedValue()
  vi.mocked(transferenciasApi.descartarContrapartidaTransferencia).mockResolvedValue()
  vi.mocked(transferenciasApi.confirmarContrapartidaTransferencia).mockResolvedValue()
})

describe('TransferenciasParaRevisar', () => {
  it('should fix the transfers counted twice when the user clicks "Corrigir agora"', async () => {
    const onAlterado = vi.fn()
    render(<TransferenciasParaRevisar clienteId="cli" onAlterado={onAlterado} />)

    expect(await screen.findByText('1 transferência(s) contada(s) duas vezes')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Corrigir agora' }))

    expect(transferenciasApi.corrigirContrapartidasTransferencia).toHaveBeenCalledWith('cli')
    expect(onAlterado).toHaveBeenCalled()
  })

  it('should drop the invented counterpart when the money did not go to that account', async () => {
    render(<TransferenciasParaRevisar clienteId="cli" />)

    await userEvent.click(await screen.findByRole('button', { name: 'Não foi para o C6 Bank' }))

    expect(transferenciasApi.descartarContrapartidaTransferencia).toHaveBeenCalledWith('t-1410')
  })

  it('should keep the counterpart when the user confirms it went to that account', async () => {
    render(<TransferenciasParaRevisar clienteId="cli" />)

    await userEvent.click(await screen.findByRole('button', { name: 'Foi para o C6 Bank mesmo' }))

    expect(transferenciasApi.confirmarContrapartidaTransferencia).toHaveBeenCalledWith('t-1410')
  })

  it('should render nothing when there is nothing to review', async () => {
    vi.mocked(transferenciasApi.listarTransferenciasParaRevisar).mockResolvedValue({ corrigiveis: [], paraDecidir: [] })

    const { container } = render(<TransferenciasParaRevisar clienteId="cli" />)

    await vi.waitFor(() => expect(transferenciasApi.listarTransferenciasParaRevisar).toHaveBeenCalled())
    expect(container).toBeEmptyDOMElement()
  })
})
