// frontend/src/pages/client/ClientContasPage.test.tsx
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react'
import ClientContasPage from './ClientContasPage'
import * as AuthContextModule from '../../contexts/AuthContext'
import * as useRegistrosHook from '../../hooks/useRegistros'
import * as contasRecorrentesApi from '../../api/contasRecorrentes'
import * as contasBancariasApi from '../../api/contasBancarias'
import * as categoriasApi from '../../api/categorias'
import { addDays, todayISO } from '../../utils/format'
import type { ContaBancaria, Registro } from '../../types'

vi.mock('../../contexts/AuthContext', async (importOriginal) => {
  const actual = await importOriginal<typeof AuthContextModule>()
  return { ...actual, useAuth: vi.fn() }
})
vi.mock('../../hooks/useRegistros')
vi.mock('../../api/contasRecorrentes')
vi.mock('../../api/contasBancarias')
vi.mock('../../api/categorias')

const mockUser = { usuarioId: 'u1', nomeUsuario: 'cli1', perfil: 'cliente' as const, nomeCompleto: 'Cliente Um', nomeEstabelecimento: '', token: 'tok' }

// bare `localStorage` não está disponível neste ambiente de teste (jsdom/vitest) — stub simples
// em memória, consistente com o que ConquistarMetaModal.test.tsx já usa. Rodada de ajustes Money
// Vision (item 3.1): o estado aberto/fechado de cada seção persiste em localStorage por cliente —
// sem recriar a cada teste, um teste que abre/fecha uma seção vazaria esse estado pro próximo
// (mesmo clienteId mockado em todos).
function criarLocalStorageFake() {
  const dados = new Map<string, string>()
  return {
    getItem: (k: string) => dados.get(k) ?? null,
    setItem: (k: string, v: string) => { dados.set(k, v) },
    removeItem: (k: string) => { dados.delete(k) },
    clear: () => { dados.clear() },
  }
}
beforeEach(() => { vi.stubGlobal('localStorage', criarLocalStorageFake()) })

function mockHooks(registros: Registro[], overrides: Partial<ReturnType<typeof useRegistrosHook.useRegistros>> = {}) {
  vi.mocked(AuthContextModule.useAuth).mockReturnValue({
    user: mockUser,
    login: vi.fn(),
    logout: vi.fn(),
  })
  vi.mocked(contasRecorrentesApi.listarContasRecorrentes).mockResolvedValue([])
  vi.mocked(contasRecorrentesApi.listarSugestoesRecorrencia).mockResolvedValue([])
  vi.mocked(contasBancariasApi.listarContasBancarias).mockResolvedValue([])
  vi.mocked(categoriasApi.listarCategorias).mockResolvedValue({ entradas: [], saidas: [] })
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

// Dia `dia` do mês ANTERIOR ao atual — sempre vencido, sem depender de quando o teste roda e sem
// cair na pegadinha de toISOString() (UTC) empurrar a data perto da virada do dia.
function isoMesAnterior(dia: number): string {
  const hoje = new Date(todayISO() + 'T12:00:00')
  const d = new Date(hoje.getFullYear(), hoje.getMonth() - 1, dia, 12)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

test('exclui a conta certa quando dois registros compartilham a mesma data sem conta bancária vinculada', async () => {
  // Reproduz o bug relatado: dois RegistroDiario distintos (ids diferentes) na MESMA data, ambos
  // sem contaBancariaId (registros legados) — identificar o registro de origem por (data,
  // contaBancariaId) é ambíguo aqui; só o id do registro resolve sem ambiguidade. Vencimento hoje
  // (não atrasado) pra aparecer na aba "A Receber" sem precisar trocar o modo da lista.
  const hoje = todayISO()
  const registroA = criarRegistro({
    id: 'reg-A', data: hoje, saldoInicio: 100,
    contasAReceber: [{ descricao: 'Pollye', valor: 600, dataVencimento: hoje, pago: false }],
  })
  const registroB = criarRegistro({
    id: 'reg-B', data: hoje, saldoInicio: 200,
    contasAReceber: [{ descricao: 'Outro Cliente', valor: 999, dataVencimento: hoje, pago: false }],
  })
  const salvar = vi.fn().mockResolvedValue({})
  mockHooks([registroA, registroB], { salvar })

  render(<ClientContasPage />)

  // Exclui "Outro Cliente", que vive no SEGUNDO registro (registroB) — um origemRegistro() que usa
  // apenas (data, contaBancariaId) encontraria sempre o PRIMEIRO registro que bate (registroA, via
  // Array.find), já que ambos têm a mesma data e contaBancariaId undefined. Esse é o caso que expõe
  // o bug: a exclusão "funcionava" sem erro, mas mexia no registro errado (A), deixando B intacto.
  const linhaOutroCliente = await screen.findByText('Outro Cliente')
  fireEvent.click(linhaOutroCliente.closest('.conta-item')!.querySelector('.cb-btn-inativar')!)

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
  // Vencida: só aparece no modo "Atrasadas" (o modo "mês" tira as atrasadas pra não duplicar
  // com o aviso vermelho do topo — Parte 3, item 2).
  fireEvent.click(screen.getByRole('button', { name: 'Atrasadas' }))

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

test('mostra os totais pendentes do mês a receber e a pagar no topo', async () => {
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
  const totalReceber = screen.getByText(/A receber em/).closest('.contas-total-card') as HTMLElement
  const totalPagar = screen.getByText(/A pagar em/).closest('.contas-total-card') as HTMLElement
  expect(within(totalReceber).getByText('R$ 750,00')).toBeInTheDocument() // 500 + 250
  expect(within(totalPagar).getByText('R$ 300,00')).toBeInTheDocument()
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

// ── Parte 3 (Money Vision): seletor de mês, atrasadas sempre no topo, pagas/recebidas recolhidas ──

// Parte 4 (Money Vision): atrasada não some da lista — continua listada dentro do mês do seu
// próprio vencimento (e conta no total daquele mês), além de continuar no aviso do topo e no
// modo "Atrasadas".
test('atrasada aparece dentro do mês do seu vencimento e conta no total daquele mês', async () => {
  const vencimentoMesAnterior = isoMesAnterior(15)
  const registro = criarRegistro({
    contasAPagar: [{ descricao: 'Conta velha', valor: 400, dataVencimento: vencimentoMesAnterior, pago: false }],
  })
  mockHooks([registro])

  render(<ClientContasPage />)

  expect(await screen.findByText(/1 conta\(s\) atrasada\(s\) · R\$ 400,00/)).toBeInTheDocument()
  // Mês atual (padrão) não é o mês do vencimento dela — ainda não aparece aqui. "A Receber" e "A
  // Pagar" ficam ambas abertas por padrão (item 3.1) — sem contas a receber no fixture, as duas
  // seções mostram "Nenhuma pendente.".
  expect(screen.getAllByText('Nenhuma pendente.')).toHaveLength(2)
  expect(screen.queryByText('Conta velha')).not.toBeInTheDocument()

  // Navega pro mês do próprio vencimento: aparece lá, e conta no total daquele mês.
  fireEvent.click(screen.getByRole('button', { name: '‹' }))
  expect(await screen.findByText('Conta velha')).toBeInTheDocument()
  const totalPagar = screen.getByText(/A pagar em/).closest('.contas-total-card') as HTMLElement
  expect(within(totalPagar).getByText('R$ 400,00')).toBeInTheDocument()

  // Continua aparecendo também no modo "Atrasadas", pra quem quer ver todas de uma vez.
  fireEvent.click(screen.getByRole('button', { name: 'Atrasadas' }))
  expect(await screen.findByText('Conta velha')).toBeInTheDocument()
})

test('pendente de um mês futuro não aparece no modo "mês atual", mas aparece em "Tudo"', async () => {
  const dataFutura = addDays(todayISO(), 60) // ~2 meses adiante, não vencida
  const registro = criarRegistro({
    contasAPagar: [{ descricao: 'Pagamento futuro distante', valor: 200, dataVencimento: dataFutura, pago: false }],
  })
  mockHooks([registro])

  render(<ClientContasPage />)
  // "A Receber" e "A Pagar" ficam ambas abertas por padrão (item 3.1) — sem contas a receber no
  // fixture, as duas seções mostram "Nenhuma pendente.".
  await waitFor(() => expect(screen.getAllByText('Nenhuma pendente.')).toHaveLength(2))
  expect(screen.queryByText('Pagamento futuro distante')).not.toBeInTheDocument()

  fireEvent.click(screen.getByRole('button', { name: 'Tudo' }))
  expect(await screen.findByText('Pagamento futuro distante')).toBeInTheDocument()
})

test('aba "Pagas/Recebidas" mostra resumo recolhido por padrão e expande ao clicar', async () => {
  const registro = criarRegistro({
    contasAReceber: [{ descricao: 'Recebido este mês', valor: 150, dataVencimento: todayISO(), pago: true, dataBaixa: todayISO() }],
  })
  mockHooks([registro])

  render(<ClientContasPage />)
  fireEvent.click(screen.getByRole('button', { name: /Pagas\/Recebidas/ }))

  expect(await screen.findByText(/1 recebida\(s\) em/)).toBeInTheDocument()
  expect(screen.queryByText('Recebido este mês')).not.toBeInTheDocument()

  fireEvent.click(screen.getByText(/1 recebida\(s\) em/))
  expect(await screen.findByText('Recebido este mês')).toBeInTheDocument()
})

test('aba "Recorrentes" mostra as recorrências ativas separadas das pendências avulsas', async () => {
  mockHooks([criarRegistro({})])
  vi.mocked(contasRecorrentesApi.listarContasRecorrentes).mockResolvedValue([{
    id: 'rec-1', clienteId: 'u1', descricao: 'Aluguel', valor: 1000, tipo: 'Pagar',
    periodicidade: 'Mensal', dataInicio: '2026-01-01', categoria: 'Aluguel', ativo: true,
    contaBancariaId: 'conta-1', valorVariavel: false, criadoEm: '2026-01-01T00:00:00Z',
  }])

  render(<ClientContasPage />)
  expect(screen.queryByText('Aluguel')).not.toBeInTheDocument()

  fireEvent.click(screen.getByRole('button', { name: /Recorrentes/ }))
  expect(await screen.findByText('Aluguel')).toBeInTheDocument()
})
