import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import ClientDrePage from './ClientDrePage'
import * as AuthContextModule from '../../contexts/AuthContext'
import * as metricasApi from '../../api/metricas'
import * as contasBancariasApi from '../../api/contasBancarias'
import * as useRegistrosModule from '../../hooks/useRegistros'
import type { Dre, DreBloco } from '../../api/metricas'
import type { Registro } from '../../types'

vi.mock('../../contexts/AuthContext', async (importOriginal) => {
  const actual = await importOriginal<typeof AuthContextModule>()
  return { ...actual, useAuth: vi.fn() }
})
vi.mock('../../api/metricas', async (importOriginal) => {
  const actual = await importOriginal<typeof metricasApi>()
  return { ...actual, obterDre: vi.fn() }
})
vi.mock('../../api/contasBancarias', async (importOriginal) => {
  const actual = await importOriginal<typeof contasBancariasApi>()
  return { ...actual, listarContasBancarias: vi.fn() }
})
vi.mock('../../hooks/useRegistros', async (importOriginal) => {
  const actual = await importOriginal<typeof useRegistrosModule>()
  return { ...actual, useRegistros: vi.fn() }
})

const mockUser = { usuarioId: 'u1', nomeUsuario: 'cli1', perfil: 'cliente' as const, nomeCompleto: 'C', nomeEstabelecimento: '', token: 'tok' }

function mockDre(receitaBruta: number): Dre {
  return {
    receitaBruta,
    gruposDespesa: [],
    totalDespesas: 0,
    resultado: receitaBruta,
    margem: null,
    receitaBrutaPercentual: 100,
    deducoes: { total: 0, percentual: 0, categorias: [] },
    receitaLiquida: receitaBruta,
    receitaLiquidaPercentual: 100,
    custosVariaveis: { total: 0, percentual: 0, categorias: [] },
    margemContribuicao: receitaBruta,
    margemContribuicaoPercentual: 100,
    despesasFixas: { total: 0, percentual: 0, categorias: [] },
    resultadoOperacional: receitaBruta,
    resultadoOperacionalPercentual: 100,
    receitaFinanceira: { total: 0, percentual: 0, categorias: [] },
    despesasNaoOperacionais: { total: 0, percentual: 0, categorias: [] },
    naoClassificado: { total: 0, percentual: 0, categorias: [] },
    atividadesInvestimento: { total: 0, percentual: 0, categorias: [] },
    atividadesFinanciamento: { total: 0, percentual: 0, categorias: [] },
    retiradasSocios: { total: 0, percentual: 0, categorias: [] },
    resultadoLiquido: receitaBruta,
    resultadoLiquidoPercentual: 100,
    blocos: [],
    pontoEquilibrio: null,
    evolucaoResultadoLiquido: null,
  }
}

function mockDreComBloco(receitaBruta: number, bloco: DreBloco): Dre {
  return { ...mockDre(receitaBruta), blocos: [bloco] }
}

function registro(data: string): Registro {
  return {
    id: data, clienteId: 'u1', data, saldoInicio: 0,
    entradas: [], saidas: [], contasAReceber: [], contasAPagar: [],
    saldoConfirmado: 0, saldoCalculado: 0, criadoEm: `${data}T00:00:00Z`,
  }
}

const mockRegistrosReturn = {
  registros: [] as Registro[],
  loading: false,
  erro: '',
  salvar: vi.fn(),
  excluir: vi.fn(),
  buscarPorData: vi.fn(),
  recarregar: vi.fn(),
}

function mockHooks(registros: Registro[]) {
  vi.mocked(AuthContextModule.useAuth).mockReturnValue({ user: mockUser, login: vi.fn(), logout: vi.fn() })
  vi.mocked(contasBancariasApi.listarContasBancarias).mockResolvedValue([])
  vi.mocked(useRegistrosModule.useRegistros).mockReturnValue({ ...mockRegistrosReturn, registros })
  vi.mocked(metricasApi.obterDre).mockImplementation(async (_clienteId, de) => mockDre(de.endsWith('-01') ? 500 : 0))
}

test('mes atual sem lançamento nenhum: pula pro ultimo mes com movimento', async () => {
  const hoje = new Date()
  const anoAtual = hoje.getFullYear()
  const mesAtual = hoje.getMonth() + 1
  // Mes passado tem dado real; mes atual nao tem nenhum registro ainda.
  const mesPassadoRef = new Date(anoAtual, mesAtual - 1 - 1, 15)
  const mesPassadoIso = `${mesPassadoRef.getFullYear()}-${String(mesPassadoRef.getMonth() + 1).padStart(2, '0')}-15`

  mockHooks([registro(mesPassadoIso)])
  render(<ClientDrePage />)

  await waitFor(() => {
    expect(screen.getByText(new RegExp(`${mesPassadoRef.getFullYear()}`))).toBeInTheDocument()
  })
})

test('mes atual com lançamento: permanece no mes atual', async () => {
  const hoje = new Date()
  const anoAtual = hoje.getFullYear()
  const mesAtual = hoje.getMonth() + 1
  const hojeIso = `${anoAtual}-${String(mesAtual).padStart(2, '0')}-01`

  mockHooks([registro(hojeIso)])
  render(<ClientDrePage />)

  await waitFor(() => {
    expect(screen.getByText(new RegExp(`${anoAtual}`))).toBeInTheDocument()
  })
  expect(vi.mocked(metricasApi.obterDre)).toHaveBeenCalledWith('u1', hojeIso, expect.stringContaining(`${anoAtual}-${String(mesAtual).padStart(2, '0')}`), undefined)
})

// ── Parte 1 (Money Vision): "Comparar períodos" como tabela única ──────────────────────────────

test('Comparar períodos: grupo e categoria aparecem UMA vez, com um valor por período na mesma linha', async () => {
  mockHooks([registro('2026-10-01')])
  const blocoSet: DreBloco = {
    bloco: 'DESPESAS OPERACIONAIS', total: -1000, percentual: -10,
    grupos: [{ nome: 'Aluguel e Ocupação', total: -1000, percentual: -10, categorias: [{ nome: 'Aluguel', total: -1000, percentual: -10 }] }],
  }
  const blocoOut: DreBloco = {
    bloco: 'DESPESAS OPERACIONAIS', total: -1200, percentual: -12,
    grupos: [{ nome: 'Aluguel e Ocupação', total: -1200, percentual: -12, categorias: [{ nome: 'Aluguel', total: -1200, percentual: -12 }] }],
  }
  vi.mocked(metricasApi.obterDre).mockImplementation(async (_clienteId, de) =>
    mockDreComBloco(10000, de.startsWith('2026-09') ? blocoSet : blocoOut),
  )

  const user = userEvent.setup()
  render(<ClientDrePage />)

  await user.click(await screen.findByLabelText('Comparar períodos'))
  await user.selectOptions(screen.getByDisplayValue('Últimos 3'), 'Últimos 2')

  await user.click(await screen.findByText(/DESPESAS OPERACIONAIS/))
  const linhaGrupo = (await screen.findByText('Aluguel e Ocupação')).closest('.dre-tabela-linha') as HTMLElement
  expect(screen.getAllByText('Aluguel e Ocupação')).toHaveLength(1)

  await user.click(within(linhaGrupo).getByText('Aluguel e Ocupação'))
  const linhasCategoria = screen.getAllByText('Aluguel')
  expect(linhasCategoria).toHaveLength(1)
  const linhaCategoria = linhasCategoria[0].closest('.dre-tabela-linha') as HTMLElement

  // Os dois períodos (Set e Out) aparecem como células NA MESMA linha — não empilhados.
  expect(within(linhaCategoria).getByText('-R$ 1.000,00')).toBeInTheDocument()
  expect(within(linhaCategoria).getByText('-R$ 1.200,00')).toBeInTheDocument()
})

test('Comparar períodos: mostra colunas de Variação e Média', async () => {
  mockHooks([registro('2026-10-01')])
  const user = userEvent.setup()
  render(<ClientDrePage />)

  await user.click(await screen.findByLabelText('Comparar períodos'))

  expect(await screen.findByText('Variação')).toBeInTheDocument()
  expect(screen.getByText('Média')).toBeInTheDocument()
})
