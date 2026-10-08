// frontend/src/pages/client/ClientContasPage.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import ClientContasPage from './ClientContasPage'
import * as AuthContextModule from '../../contexts/AuthContext'
import * as useRegistrosHook from '../../hooks/useRegistros'
import * as contasRecorrentesApi from '../../api/contasRecorrentes'
import * as contasBancariasApi from '../../api/contasBancarias'
import { addDays, todayISO } from '../../utils/format'
import type { ContaBancaria, Registro } from '../../types'

vi.mock('../../contexts/AuthContext', async (importOriginal) => {
  const actual = await importOriginal<typeof AuthContextModule>()
  return { ...actual, useAuth: vi.fn() }
})
vi.mock('../../hooks/useRegistros')
vi.mock('../../api/contasRecorrentes')
vi.mock('../../api/contasBancarias')

const mockUser = { usuarioId: 'u1', nomeUsuario: 'cli1', perfil: 'cliente' as const, nomeCompleto: 'Cliente Um', nomeEstabelecimento: '', token: 'tok' }

function mockHooks(registros: Registro[], overrides: Partial<ReturnType<typeof useRegistrosHook.useRegistros>> = {}) {
  vi.mocked(AuthContextModule.useAuth).mockReturnValue({
    user: mockUser,
    login: vi.fn(),
    logout: vi.fn(),
  })
  vi.mocked(contasRecorrentesApi.listarContasRecorrentes).mockResolvedValue([])
  vi.mocked(contasBancariasApi.listarContasBancarias).mockResolvedValue([])
  vi.mocked(useRegistrosHook.useRegistros).mockReturnValue({
    registros,
    loading: false,
    erro: '',
    salvar: vi.fn().mockResolvedValue({}),
    excluir: vi.fn(),
    buscarPorData: vi.fn().mockResolvedValue(null),
    recarregar: vi.fn(),
    ...overrides,
  } as ReturnType<typeof useRegistrosHook.useRegistros>)
}

function criarRegistro(overrides: Partial<Registro>): Registro {
  return {
    id: 'reg-default',
    clienteId: 'u1',
    contaBancariaId: undefined,
    data: '2026-09-10',
    saldoInicio: 0,
    entradas: [],
    saidas: [],
    contasAReceber: [],
    contasAPagar: [],
    saldoConfirmado: 0,
    saldoCalculado: 0,
    criadoEm: '2026-09-10T00:00:00Z',
    ...overrides,
  }
}

test('exclui a conta certa quando dois registros compartilham a mesma data sem conta bancária vinculada', async () => {
  // Reproduz o bug relatado: dois RegistroDiario distintos (ids diferentes) na MESMA data, ambos
  // sem contaBancariaId (registros legados) — identificar o registro de origem por (data,
  // contaBancariaId) é ambíguo aqui; só o id do registro resolve sem ambiguidade.
  const registroA = criarRegistro({
    id: 'reg-A', data: '2026-09-10', saldoInicio: 100,
    contasAReceber: [{ descricao: 'Pollye', valor: 600, dataVencimento: '2026-09-10', pago: false }],
  })
  const registroB = criarRegistro({
    id: 'reg-B', data: '2026-09-10', saldoInicio: 200,
    contasAReceber: [{ descricao: 'Outro Cliente', valor: 999, dataVencimento: '2026-09-10', pago: false }],
  })
  const salvar = vi.fn().mockResolvedValue({})
  mockHooks([registroA, registroB], { salvar })

  render(<ClientContasPage />)

  // Exclui "Outro Cliente", que vive no SEGUNDO registro (registroB) — um origemRegistro() que usa
  // apenas (data, contaBancariaId) encontraria sempre o PRIMEIRO registro que bate (registroA, via
  // Array.find), já que ambos têm a mesma data e contaBancariaId undefined. Esse é o caso que expõe
  // o bug: a exclusão "funcionava" sem erro, mas mexia no registro errado (A), deixando B intacto.
  const linhaOutroCliente = screen.getByText('Outro Cliente').closest('.conta-item')!
  fireEvent.click(linhaOutroCliente.querySelector('.cb-btn-inativar')!)

  // Confirma no modal — o botão "Excluir" do modal é o último da tela (renderizado por último)
  const botoesExcluir = screen.getAllByRole('button', { name: 'Excluir' })
  fireEvent.click(botoesExcluir[botoesExcluir.length - 1])

  await waitFor(() => expect(salvar).toHaveBeenCalledTimes(1))

  const payload = salvar.mock.calls[0][0]
  // Tem que ter salvo o registro B (saldoInicio=200), não o A (saldoInicio=100) — confirma que o
  // registro de origem correto foi identificado, não só que "a lista ficou vazia" (os dois
  // registros têm 1 item cada no índice 0, então aquela asserção sozinha não distingue A de B).
  expect(payload.saldoInicio).toBe(200)
  expect(payload.contasAReceber).toEqual([])
})

// ── Item 2.5 ──────────────────────────────────────────────────────────────────────────────────

function criarConta(overrides: Partial<ContaBancaria> = {}): ContaBancaria {
  return {
    id: 'conta-1', clienteId: 'u1', nome: 'Caixa', tipo: 'Caixa',
    saldoInicial: 0, saldoAtual: 0, entradasMes: 0, saidasMes: 0,
    pendentesCategorizacao: 0, ativa: true, dataCriacao: '2026-01-01',
    ...overrides,
  }
}

test('conta a pagar vencida aparece destacada com "atrasada há X dias"', async () => {
  const vencimento = addDays(todayISO(), -5)
  const registro = criarRegistro({
    contasAPagar: [{ descricao: 'Fornecedor atrasado', valor: 300, dataVencimento: vencimento, pago: false }],
  })
  mockHooks([registro])

  render(<ClientContasPage />)

  const linha = await screen.findByText('Fornecedor atrasado')
  const item = linha.closest('.conta-item')!
  expect(item).toHaveClass('vencida')
  expect(item).toHaveTextContent('atrasada há 5 dias')
})

test('conta pendente com vencimento hoje não é marcada como atrasada', async () => {
  const registro = criarRegistro({
    contasAPagar: [{ descricao: 'Vence hoje', valor: 100, dataVencimento: todayISO(), pago: false }],
  })
  mockHooks([registro])

  render(<ClientContasPage />)

  const linha = await screen.findByText('Vence hoje')
  expect(linha.closest('.conta-item')).not.toHaveClass('vencida')
})

test('mostra os totais pendentes a receber e a pagar no topo', async () => {
  const registro = criarRegistro({
    contasAReceber: [
      { descricao: 'Cliente A', valor: 500, dataVencimento: todayISO(), pago: false },
      { descricao: 'Cliente B', valor: 250, dataVencimento: todayISO(), pago: false },
    ],
    contasAPagar: [{ descricao: 'Fornecedor', valor: 300, dataVencimento: todayISO(), pago: false }],
  })
  mockHooks([registro])

  render(<ClientContasPage />)

  await screen.findByText('Cliente A')
  const totalReceber = screen.getByText('A receber').closest('.contas-total-card')!
  const totalPagar = screen.getByText('A pagar').closest('.contas-total-card')!
  expect(totalReceber).toHaveTextContent('R$ 750,00') // 500 + 250
  expect(totalPagar).toHaveTextContent('R$ 300,00')
})

test('filtro por conta esconde pendências de outras contas', async () => {
  const contaA = criarConta({ id: 'conta-A', nome: 'Conta A' })
  const contaB = criarConta({ id: 'conta-B', nome: 'Conta B' })
  const registro = criarRegistro({
    contasAPagar: [
      { descricao: 'Da conta A', valor: 100, dataVencimento: todayISO(), pago: false, contaBancariaId: 'conta-A' },
      { descricao: 'Da conta B', valor: 200, dataVencimento: todayISO(), pago: false, contaBancariaId: 'conta-B' },
    ],
  })
  mockHooks([registro])
  vi.mocked(contasBancariasApi.listarContasBancarias).mockResolvedValue([contaA, contaB])

  render(<ClientContasPage />)
  await screen.findByText('Da conta A')
  expect(screen.getByText('Da conta B')).toBeInTheDocument()

  fireEvent.change(screen.getByLabelText('Conta'), { target: { value: 'conta-A' } })

  expect(screen.getByText('Da conta A')).toBeInTheDocument()
  expect(screen.queryByText('Da conta B')).not.toBeInTheDocument()
})
