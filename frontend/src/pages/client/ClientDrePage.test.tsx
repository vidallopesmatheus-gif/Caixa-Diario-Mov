import { render, screen, waitFor } from '@testing-library/react'
import ClientDrePage from './ClientDrePage'
import * as AuthContextModule from '../../contexts/AuthContext'
import * as metricasApi from '../../api/metricas'
import * as contasBancariasApi from '../../api/contasBancarias'
import * as useRegistrosModule from '../../hooks/useRegistros'
import type { Dre } from '../../api/metricas'
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
    resultadoLiquido: receitaBruta,
    resultadoLiquidoPercentual: 100,
    blocos: [],
    pontoEquilibrio: null,
    evolucaoResultadoLiquido: null,
  }
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
