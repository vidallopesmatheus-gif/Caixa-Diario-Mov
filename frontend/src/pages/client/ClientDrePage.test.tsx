import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import ClientDrePage from './ClientDrePage'
import * as AuthContextModule from '../../contexts/AuthContext'
import * as metricasApi from '../../api/metricas'
import * as contasBancariasApi from '../../api/contasBancarias'
import * as useRegistrosModule from '../../hooks/useRegistros'
import type { Dre, DreBloco } from '../../api/metricas'
import type { Registro } from '../../types'
import { fmtBRL } from '../../utils/format'

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

// Item 3 da Parte 3 (Money Vision): Média tem que ser média(valor) ÷ média(receita), não a média
// dos percentuais — com receitas bem diferentes entre os períodos, as duas contas dão números
// diferentes (aqui: -13,3% pela conta certa vs. -15,0% pela média simples dos percentuais, que o
// componente já não calcula mais).
test('Comparar períodos: coluna Média usa média(valor) ÷ média(receita), não a média dos percentuais', async () => {
  mockHooks([registro('2026-10-01')])
  const blocoSet: DreBloco = {
    bloco: 'DESPESAS OPERACIONAIS', total: -1000, percentual: -10,
    grupos: [{ nome: 'Aluguel e Ocupação', total: -1000, percentual: -10, categorias: [{ nome: 'Aluguel', total: -1000, percentual: -10 }] }],
  }
  const blocoOut: DreBloco = {
    bloco: 'DESPESAS OPERACIONAIS', total: -1000, percentual: -20,
    grupos: [{ nome: 'Aluguel e Ocupação', total: -1000, percentual: -20, categorias: [{ nome: 'Aluguel', total: -1000, percentual: -20 }] }],
  }
  vi.mocked(metricasApi.obterDre).mockImplementation(async (_clienteId, de) =>
    mockDreComBloco(de.startsWith('2026-09') ? 10000 : 5000, de.startsWith('2026-09') ? blocoSet : blocoOut),
  )

  const user = userEvent.setup()
  render(<ClientDrePage />)

  await user.click(await screen.findByLabelText('Comparar períodos'))
  await user.selectOptions(screen.getByDisplayValue('Últimos 3'), 'Últimos 2')

  const linhaBloco = (await screen.findByText(/DESPESAS OPERACIONAIS/)).closest('.dre-tabela-linha') as HTMLElement
  const celulaMedia = linhaBloco.querySelector('.dre-tabela-media-col') as HTMLElement
  // Média(valor) = -1000 nos dois períodos; Média(receita) = (10000+5000)/2 = 7500.
  // -1000 ÷ 7500 × 100 = -13,3% — a média simples dos percentuais (-10% e -20%) daria -15,0%.
  expect(within(celulaMedia).getByText('-13,3%')).toBeInTheDocument()
})

// Item 2 da Parte 3 (Money Vision): quando o período mais recente comparado ainda está em
// andamento, a Variação não pode comparar o valor cheio dele com o valor cheio do período
// anterior (sempre pareceria uma queda forte só por ter menos dias corridos) — tem que podar o
// período anterior pros mesmos dias corridos e avisar com o selo "parcial".
test('Comparar períodos: Variação do mês em andamento usa selo "parcial" e os mesmos dias do mês anterior', async () => {
  mockHooks([registro('2026-10-01')])
  // Setembro (anterior, mês fechado, 30 dias): -600. Outubro (atual, em andamento até hoje): -400.
  const blocoSet: DreBloco = {
    bloco: 'DESPESAS OPERACIONAIS', total: -600, percentual: -6,
    grupos: [{ nome: 'Aluguel e Ocupação', total: -600, percentual: -6, categorias: [{ nome: 'Aluguel', total: -600, percentual: -6 }] }],
  }
  const blocoOut: DreBloco = {
    bloco: 'DESPESAS OPERACIONAIS', total: -400, percentual: -4,
    grupos: [{ nome: 'Aluguel e Ocupação', total: -400, percentual: -4, categorias: [{ nome: 'Aluguel', total: -400, percentual: -4 }] }],
  }
  vi.mocked(metricasApi.obterDre).mockImplementation(async (_clienteId, de) =>
    mockDreComBloco(10000, de.startsWith('2026-09') ? blocoSet : blocoOut),
  )

  const user = userEvent.setup()
  render(<ClientDrePage />)

  await user.click(await screen.findByLabelText('Comparar períodos'))
  await user.selectOptions(screen.getByDisplayValue('Últimos 3'), 'Últimos 2')

  const linhaBloco = (await screen.findByText(/DESPESAS OPERACIONAIS/)).closest('.dre-tabela-linha') as HTMLElement
  const celulaVariacao = linhaBloco.querySelector('.dre-tabela-var-col') as HTMLElement
  expect(within(celulaVariacao).getByText('parcial')).toBeInTheDocument()
  // Setembro podado pros mesmos dias corridos de outubro até hoje: -600 × (diasElapsed/30).
  // Sem a poda, a comparação seria -400 vs. -600 (R$ 200 "de melhora") — com a poda, o resultado
  // muda de sinal: outubro está PIOR que o mesmo recorte de setembro, não melhor.
  const hoje = new Date()
  const diasElapsed = hoje.getDate()
  const anteriorPodado = -600 * (diasElapsed / 30)
  const rsEsperado = -400 - anteriorPodado
  // getByText não casa aqui porque o valor convive com o <span> do selo "parcial" no mesmo nó —
  // confere direto o texto do nó de valor (ignorando o selo, que já foi checado acima).
  expect(celulaVariacao.querySelector('.dre-tabela-cel-valor')?.textContent).toContain(fmtBRL(rsEsperado))
})

// Item 1 da Parte 3 (Money Vision): "Não Classificados" nunca pode desaparecer da tela — mesmo
// com total zerado (sem lançamento sem categoria no período), a linha continua visível mostrando
// R$ 0,00, tanto no período único quanto em "Comparar períodos".
test('"Não Classificados" continua visível (R$ 0,00) quando não há lançamento sem categoria', async () => {
  mockHooks([registro('2026-10-01')])
  render(<ClientDrePage />)

  const label = await screen.findByText('(-) NÃO CLASSIFICADOS')
  const linha = label.closest('.dre-linha-bloco') as HTMLElement
  expect(within(linha).getByText('R$ 0,00')).toBeInTheDocument()
})

test('Comparar períodos: "Não Classificados" continua visível (R$ 0,00) em todos os períodos', async () => {
  mockHooks([registro('2026-10-01')])
  const user = userEvent.setup()
  render(<ClientDrePage />)

  await user.click(await screen.findByLabelText('Comparar períodos'))

  const label = await screen.findByText('(-) NÃO CLASSIFICADOS')
  const linha = label.closest('.dre-tabela-linha') as HTMLElement
  expect(within(linha).getAllByText('R$ 0,00').length).toBeGreaterThan(0)
})
