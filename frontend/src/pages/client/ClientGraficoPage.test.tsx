// frontend/src/pages/client/ClientGraficoPage.test.tsx
import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import ClientGraficoPage from './ClientGraficoPage'
import * as AuthContextModule from '../../contexts/AuthContext'
import * as metricasApi from '../../api/metricas'
import * as useRegistrosModule from '../../hooks/useRegistros'
import type { IndicadoresDecisao } from '../../api/metricas'
import type { Registro } from '../../types'

vi.mock('../../contexts/AuthContext', async (importOriginal) => {
  const actual = await importOriginal<typeof AuthContextModule>()
  return { ...actual, useAuth: vi.fn() }
})
vi.mock('../../api/metricas', async (importOriginal) => {
  const actual = await importOriginal<typeof metricasApi>()
  return { ...actual, obterIndicadores: vi.fn() }
})
vi.mock('../../hooks/useRegistros', async (importOriginal) => {
  const actual = await importOriginal<typeof useRegistrosModule>()
  return { ...actual, useRegistros: vi.fn() }
})

// Recharts usa ResizeObserver; precisamos de um stub
window.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
}

const mockUser = { usuarioId: 'u1', nomeUsuario: 'cli1', perfil: 'cliente' as const, nomeCompleto: 'C', nomeEstabelecimento: '', token: 'tok' }

function mesesEvolucao(n: number, receitaBase = 1000) {
  const hoje = new Date()
  const lista = []
  for (let i = n - 1; i >= 0; i--) {
    const ref = new Date(hoje.getFullYear(), hoje.getMonth() - i, 1)
    const mes = `${ref.getFullYear()}-${String(ref.getMonth() + 1).padStart(2, '0')}`
    lista.push({ mes, receita: receitaBase, custos: 400, lucro: receitaBase - 400, saldo: 5000 })
  }
  return lista
}

const mockIndicadores: IndicadoresDecisao = {
  dre: {
    receitaBruta: 1000,
    gruposDespesa: [{ grupo: 'Despesas Administrativas', total: 400, categorias: [{ nome: 'Aluguel', total: 400, percentual: 40 }] }],
    totalDespesas: 400,
    resultado: 600,
    margem: 60,
    receitaBrutaPercentual: 100,
    deducoes: { total: 0, percentual: 0, categorias: [] },
    receitaLiquida: 1000,
    receitaLiquidaPercentual: 100,
    custosVariaveis: { total: 0, percentual: 0, categorias: [] },
    margemContribuicao: 1000,
    margemContribuicaoPercentual: 100,
    despesasFixas: { total: 400, percentual: 40, categorias: [{ nome: 'Aluguel', total: 400, percentual: 40 }] },
    resultadoOperacional: 600,
    resultadoOperacionalPercentual: 60,
    receitaFinanceira: { total: 0, percentual: 0, categorias: [] },
    despesasNaoOperacionais: { total: 0, percentual: 0, categorias: [] },
    naoClassificado: { total: 0, percentual: 0, categorias: [] },
    resultadoLiquido: 600,
    resultadoLiquidoPercentual: 60,
  },
  custoFixo: 400,
  custoVariavel: 0,
  custoNaoClassificado: 0,
  rankingCategorias: [
    { nome: 'Aluguel', grupo: 'Despesas Administrativas', total: 400, percentualReceita: 40, mediaMesesAnteriores: 300, variacaoPercentual: 20 },
  ],
  evolucao: mesesEvolucao(13),
  mesesComAtividade: 13,
  variacaoReceitaMesAnterior: 10,
  variacaoReceitaAnoAnterior: 25,
  pontoEquilibrio: {
    disponivel: true, motivoIndisponivel: null, valorMensal: 500, valorPorDiaUtil: 25,
    diasUteisNoMes: 20, receitaAtual: 1000, distancia: 500, distanciaPercentual: 100,
  },
  folegoCaixa: {
    disponivel: true, motivoIndisponivel: null, saldoDisponivel: 3000,
    custoFixoMedioMensal: 400, meses: 7.5, faixa: 'confortavel',
  },
  custoFixoMensal: mesesEvolucao(6).map(e => ({ mes: e.mes, receita: e.receita, custoFixo: 400, percentual: 40 })),
  prazoRecebimento: { disponivel: true, motivoIndisponivel: null, mediaDias: 2, quantidadeAmostras: 8 },
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

function mockHooks(indicadores: IndicadoresDecisao = mockIndicadores, registros: Registro[] = []) {
  vi.mocked(AuthContextModule.useAuth).mockReturnValue({ user: mockUser, login: vi.fn(), logout: vi.fn() })
  vi.mocked(metricasApi.obterIndicadores).mockResolvedValue(indicadores)
  vi.mocked(useRegistrosModule.useRegistros).mockReturnValue({ ...mockRegistrosReturn, registros })
}

test('exibe loading enquanto carrega', () => {
  vi.mocked(AuthContextModule.useAuth).mockReturnValue({ user: mockUser, login: vi.fn(), logout: vi.fn() })
  vi.mocked(metricasApi.obterIndicadores).mockReturnValue(new Promise(() => {})) // never resolves
  vi.mocked(useRegistrosModule.useRegistros).mockReturnValue(mockRegistrosReturn)
  render(<ClientGraficoPage />)
  expect(screen.getByText(/Carregando/)).toBeInTheDocument()
})

test('renderiza as 3 perguntas dos blocos após carregar', async () => {
  mockHooks()
  render(<ClientGraficoPage />)
  await waitFor(() => {
    expect(screen.getByText('Meu negócio dá lucro de verdade?')).toBeInTheDocument()
    expect(screen.getByText('Qual categoria mais me custa?')).toBeInTheDocument()
    expect(screen.getByText('Estou crescendo ou estagnado?')).toBeInTheDocument()
  })
})

test('exibe a leitura textual da margem', async () => {
  mockHooks()
  render(<ClientGraficoPage />)
  await waitFor(() => expect(screen.getByText(/De cada R\$ 100 que entram, sobram R\$ 60/)).toBeInTheDocument())
})

test('exibe mensagem de histórico insuficiente quando mesesComAtividade < 3', async () => {
  mockHooks({ ...mockIndicadores, mesesComAtividade: 1 })
  render(<ClientGraficoPage />)
  await waitFor(() => expect(screen.getByText(/histórico suficiente/)).toBeInTheDocument())
})

test('não exibe mensagem de histórico insuficiente quando há 3+ meses de atividade', async () => {
  mockHooks({ ...mockIndicadores, mesesComAtividade: 3 })
  render(<ClientGraficoPage />)
  await waitFor(() => expect(screen.getByText('Meu negócio dá lucro de verdade?')).toBeInTheDocument())
  expect(screen.queryByText(/histórico suficiente/)).not.toBeInTheDocument()
})

test('clique na categoria expande os lançamentos do mês', async () => {
  const hoje = new Date()
  const dataAtual = `${hoje.getFullYear()}-${String(hoje.getMonth() + 1).padStart(2, '0')}-10`
  const registro: Registro = {
    id: 'r1', clienteId: 'u1', data: dataAtual, saldoInicio: 0,
    entradas: [], saidas: [{ descricao: 'Aluguel do escritório', valor: 400, categoria: 'Aluguel', subcategoria: '' }],
    contasAReceber: [], contasAPagar: [], saldoConfirmado: 0, saldoCalculado: 0, criadoEm: '',
  }
  mockHooks(mockIndicadores, [registro])
  render(<ClientGraficoPage />)
  await waitFor(() => expect(screen.getByText('Aluguel')).toBeInTheDocument())

  fireEvent.click(screen.getByText('Aluguel'))
  await waitFor(() => expect(screen.getByText('Aluguel do escritório')).toBeInTheDocument())
})

test('clienteIdOverride é utilizado quando fornecido', async () => {
  mockHooks()
  render(<ClientGraficoPage clienteIdOverride="outro-id" />)
  await waitFor(() => expect(screen.getByText('Meu negócio dá lucro de verdade?')).toBeInTheDocument())
  expect(vi.mocked(metricasApi.obterIndicadores)).toHaveBeenCalledWith('outro-id')
})
