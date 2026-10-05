// frontend/src/pages/client/ClientContasPage.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import ClientContasPage from './ClientContasPage'
import * as AuthContextModule from '../../contexts/AuthContext'
import * as useRegistrosHook from '../../hooks/useRegistros'
import * as contasRecorrentesApi from '../../api/contasRecorrentes'
import * as contasBancariasApi from '../../api/contasBancarias'
import type { Registro } from '../../types'

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
