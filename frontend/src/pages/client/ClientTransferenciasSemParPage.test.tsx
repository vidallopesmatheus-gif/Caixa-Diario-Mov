import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import ClientTransferenciasSemParPage from './ClientTransferenciasSemParPage'
import * as AuthContextModule from '../../contexts/AuthContext'
import * as contasBancariasApi from '../../api/contasBancarias'
import * as transferenciasApi from '../../api/transferencias'
import type { ContaBancaria } from '../../types'
import type { SugestaoTransferenciaSemPar, Transferencia } from '../../api/transferencias'

vi.mock('../../contexts/AuthContext', async (importOriginal) => {
  const actual = await importOriginal<typeof AuthContextModule>()
  return { ...actual, useAuth: vi.fn() }
})
vi.mock('../../api/contasBancarias')
vi.mock('../../api/transferencias')

const mockUser = { usuarioId: 'cliente-1', nomeUsuario: 'cli1', perfil: 'cliente' as const, nomeCompleto: 'C', nomeEstabelecimento: '', token: 'tok' }

const contaCorrente: ContaBancaria = {
  id: 'conta-corrente', clienteId: 'cliente-1', nome: 'Nubank', tipo: 'ContaCorrente',
  saldoInicial: 0, saldoAtual: 100, entradasMes: 0, saidasMes: 0, pendentesCategorizacao: 0,
  ativa: true, dataCriacao: '2026-01-01',
}
const contaInvestimento: ContaBancaria = {
  id: 'conta-investimento', clienteId: 'cliente-1', nome: 'Nubank Investimento', tipo: 'Investimento',
  saldoInicial: 0, saldoAtual: 4000, entradasMes: 0, saidasMes: 0, pendentesCategorizacao: 0,
  ativa: true, dataCriacao: '2026-01-01',
}

function sugestao(overrides: Partial<SugestaoTransferenciaSemPar>): SugestaoTransferenciaSemPar {
  return {
    contaBancariaId: 'conta-corrente', contaBancariaNome: 'Nubank', lancamentoId: 'item-1',
    data: '2026-10-08', descricao: 'Aplicação RDB', valor: -300, tipo: 'Saida',
    ...overrides,
  }
}

function renderPagina() {
  return render(
    <MemoryRouter initialEntries={['/banco/transferencias-sem-par']}>
      <Routes>
        <Route path="/banco/transferencias-sem-par" element={<ClientTransferenciasSemParPage />} />
      </Routes>
    </MemoryRouter>,
  )
}

beforeEach(() => {
  vi.clearAllMocks()
  vi.mocked(AuthContextModule.useAuth).mockReturnValue({ user: mockUser, login: vi.fn(), logout: vi.fn() } as never)
  vi.mocked(contasBancariasApi.listarContasBancarias).mockResolvedValue([contaCorrente, contaInvestimento])
})

test('mostra "nenhuma sugestão" quando a fila está vazia', async () => {
  vi.mocked(transferenciasApi.listarSugestoesTransferenciaSemPar).mockResolvedValue([])
  renderPagina()

  expect(await screen.findByText('Nenhuma sugestão pendente — tudo pareado.')).toBeInTheDocument()
})

test('pré-seleciona a conta destino sugerida e confirma usando ela', async () => {
  vi.mocked(transferenciasApi.listarSugestoesTransferenciaSemPar).mockResolvedValue([
    sugestao({ contaDestinoSugeridaId: 'conta-investimento', contaDestinoSugeridaNome: 'Nubank Investimento' }),
  ])
  const transferenciaMock: Transferencia = {
    id: 'transf-1', clienteId: 'cliente-1', contaOrigemId: 'conta-corrente', contaOrigemNome: 'Nubank',
    contaDestinoId: 'conta-investimento', contaDestinoNome: 'Nubank Investimento', data: '2026-10-08', valor: 300, criadoEm: '2026-10-08',
  }
  vi.mocked(transferenciasApi.converterLancamentoEmTransferencia).mockResolvedValue(transferenciaMock)
  const user = userEvent.setup()
  renderPagina()

  const select = await screen.findByRole('combobox') as HTMLSelectElement
  expect(select.value).toBe('conta-investimento')

  await user.click(screen.getByRole('button', { name: /Confirmar/ }))

  await waitFor(() => expect(transferenciasApi.converterLancamentoEmTransferencia).toHaveBeenCalledWith({
    contaId: 'conta-corrente', lancamentoId: 'item-1', data: '2026-10-08', tipo: 'Saida', contaContrapartidaId: 'conta-investimento',
  }))
  await waitFor(() => expect(screen.queryByText('Aplicação RDB')).not.toBeInTheDocument())
})

test('sem sugestão de destino, Confirmar fica desabilitado até o usuário escolher uma conta', async () => {
  vi.mocked(transferenciasApi.listarSugestoesTransferenciaSemPar).mockResolvedValue([sugestao({})])
  renderPagina()

  expect(await screen.findByRole('button', { name: /Confirmar/ })).toBeDisabled()
})

test('descartar remove a sugestão sem confirmar transferência', async () => {
  vi.mocked(transferenciasApi.listarSugestoesTransferenciaSemPar).mockResolvedValue([sugestao({})])
  vi.mocked(transferenciasApi.descartarSugestaoTransferencia).mockResolvedValue(undefined)
  const user = userEvent.setup()
  renderPagina()

  await user.click(await screen.findByRole('button', { name: 'Descartar' }))

  await waitFor(() => expect(transferenciasApi.descartarSugestaoTransferencia).toHaveBeenCalledWith({
    contaId: 'conta-corrente', lancamentoId: 'item-1', data: '2026-10-08',
  }))
  expect(transferenciasApi.converterLancamentoEmTransferencia).not.toHaveBeenCalled()
  await waitFor(() => expect(screen.queryByText('Aplicação RDB')).not.toBeInTheDocument())
})
