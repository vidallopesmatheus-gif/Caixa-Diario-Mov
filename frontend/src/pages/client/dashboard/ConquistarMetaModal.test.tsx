import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import ConquistarMetaModal from './ConquistarMetaModal'
import * as metasApi from '../../../api/metas'
import * as categoriasApi from '../../../api/categorias'
import * as faturasCartaoApi from '../../../api/faturasCartao'
import type { MetaAnual, ContaBancaria } from '../../../types'

vi.mock('../../../api/metas')
vi.mock('../../../api/categorias')
vi.mock('../../../api/faturasCartao')

const mockNavigate = vi.fn()
vi.mock('react-router-dom', async (importOriginal) => {
  const actual = await importOriginal<typeof import('react-router-dom')>()
  return { ...actual, useNavigate: () => mockNavigate }
})

function criarMeta(overrides: Partial<MetaAnual> = {}): MetaAnual {
  return {
    id: 'meta-1', clienteId: 'cliente-1', ano: 2026, metaReceita: 0, metaLucro: 0, mesInicio: 1, periodoMeses: 12,
    salvoEm: '2026-01-01T00:00:00Z', criadoEm: '2026-01-01T00:00:00Z', sonho: 'Carro', modoMeta: 'metodo',
    valorSonho: 30000, prazoAnos: 2, taxaRetorno: 10, totalInvestido: 0, status: 'EmAndamento',
    ...overrides,
  }
}

function criarContaCartao(overrides: Partial<ContaBancaria> = {}): ContaBancaria {
  return {
    id: 'cartao-1', clienteId: 'cliente-1', nome: 'Nubank', tipo: 'CartaoCredito',
    saldoInicial: 0, saldoAtual: 0, entradasMes: 0, saidasMes: 0, pendentesCategorizacao: 0,
    ativa: true, dataCriacao: '2026-01-01',
    ...overrides,
  }
}

function criarContaInvestimento(overrides: Partial<ContaBancaria> = {}): ContaBancaria {
  return {
    id: 'invest-1', clienteId: 'cliente-1', nome: 'Reserva', tipo: 'Investimento',
    saldoInicial: 0, saldoAtual: 5000, entradasMes: 0, saidasMes: 0, pendentesCategorizacao: 0,
    ativa: true, dataCriacao: '2026-01-01',
    ...overrides,
  }
}

// bare `localStorage` não está disponível neste ambiente de teste (jsdom/vitest) — stub simples
// em memória, só pra este arquivo, consistente com o que ConquistarMetaModal usa (getItem/setItem).
function criarLocalStorageFake() {
  const dados = new Map<string, string>()
  return {
    getItem: (k: string) => dados.get(k) ?? null,
    setItem: (k: string, v: string) => { dados.set(k, v) },
    removeItem: (k: string) => { dados.delete(k) },
    clear: () => { dados.clear() },
  }
}
let localStorageFake = criarLocalStorageFake()

beforeEach(() => {
  vi.clearAllMocks()
  localStorageFake = criarLocalStorageFake()
  vi.stubGlobal('localStorage', localStorageFake)
  vi.mocked(categoriasApi.listarCategorias).mockResolvedValue({ entradas: [], saidas: [{ nome: 'Equipamentos', tipoCusto: 'CustoVariavel' }] })
  vi.mocked(faturasCartaoApi.listarFaturasCartao).mockResolvedValue([])
  vi.mocked(faturasCartaoApi.listarBancosCartao).mockResolvedValue([])
  vi.mocked(metasApi.listarCandidatosVinculoMeta).mockResolvedValue({ parcelamentos: [], lancamentos: [] })
})

function irParaForma(label: string) {
  fireEvent.click(screen.getByRole('button', { name: label }))
}

// O componente usa <label>Texto</label> como IRMÃO do input/select (não como pai, nem com
// htmlFor/id) — getByLabelText não os associa. Acha o <label> pelo texto e pega o campo de
// formulário no mesmo contêiner (irmão direto, ou dentro do mesmo wrapper flex de Parcelas/Data).
function campoPorRotulo(texto: string | RegExp): HTMLElement {
  const label = screen.getByText(texto, { selector: 'label' })
  const campo = label.parentElement?.querySelector('input, select, textarea')
  if (!campo) throw new Error(`Nenhum campo encontrado perto do rótulo "${texto}"`)
  return campo as HTMLElement
}

// Item 6 (Money Vision): segmented control — a forma escolhida fica marcada como "ativa".
test('segmented control marca a forma de pagamento selecionada como ativa', () => {
  render(<ConquistarMetaModal meta={criarMeta()} contasBancarias={[]} onClose={vi.fn()} onConquistada={vi.fn()} />)

  const btnAVista = screen.getByRole('button', { name: 'À Vista' })
  const btnCartao = screen.getByRole('button', { name: 'Cartão' })
  expect(btnAVista.className).toContain('ativa')
  expect(btnCartao.className).not.toContain('ativa')

  irParaForma('Cartão')
  expect(btnCartao.className).toContain('ativa')
  expect(btnAVista.className).not.toContain('ativa')
})
// "Ainda não está no app — lançar agora": o fluxo que cria a compra no cartão.
function irParaCartaoLancandoAgora() {
  irParaForma('Cartão')
  fireEvent.click(screen.getByRole('button', { name: 'Ainda não — lançar agora' }))
}

// Item 3 (Money Vision): parcelas no cartão começam em 10 (sem valor lembrado antes).
test('parcelas no cartão começam em 10 por padrão', () => {
  render(<ConquistarMetaModal meta={criarMeta()} contasBancarias={[criarContaCartao()]} onClose={vi.fn()} onConquistada={vi.fn()} />)

  irParaCartaoLancandoAgora()
  expect(campoPorRotulo(/Parcelas/)).toHaveValue(10)
})

test('lembra o último número de parcelas usado da vez anterior', () => {
  localStorage.setItem('conquistarMeta.parcelasCartao', '6')
  render(<ConquistarMetaModal meta={criarMeta()} contasBancarias={[criarContaCartao()]} onClose={vi.fn()} onConquistada={vi.fn()} />)

  irParaCartaoLancandoAgora()
  expect(campoPorRotulo(/Parcelas/)).toHaveValue(6)
})

// Item 3: 1ª parcela cai no vencimento da fatura ABERTA de verdade do cartão escolhido, não na
// data da compra.
test('1ª parcela do cartão usa o vencimento da fatura aberta do cartão escolhido', async () => {
  const cartao = criarContaCartao()
  vi.mocked(faturasCartaoApi.listarFaturasCartao).mockResolvedValue([
    { id: 'f1', banco: 'Nubank', competencia: '2026-11', dataFechamento: '2026-11-01', dataVencimento: '2026-11-10', valorTotal: 0, valorPago: 0, saldoDevedor: 0, quantidadeCompras: 0, status: 'Aberta' },
  ])
  render(<ConquistarMetaModal meta={criarMeta()} contasBancarias={[cartao]} onClose={vi.fn()} onConquistada={vi.fn()} />)

  irParaCartaoLancandoAgora()
  fireEvent.change(campoPorRotulo(/Cartão$/), { target: { value: cartao.id } })

  await waitFor(() => expect(campoPorRotulo('Data da 1ª parcela')).toHaveValue('2026-11-10'))
})

// Money Vision: o valor da parcela é o que vem na fatura — nunca o valor da meta ÷ N sem confirmar.
test('resumo do cartão mostra o total pelo valor da parcela, a fatura do banco e as previstas', async () => {
  const cartao = criarContaCartao()
  render(<ConquistarMetaModal meta={criarMeta({ valorSonho: 4000 })} contasBancarias={[cartao]} onClose={vi.fn()} onConquistada={vi.fn()} />)

  irParaCartaoLancandoAgora()
  expect(campoPorRotulo('Valor de cada parcela')).toHaveValue('')
  fireEvent.change(campoPorRotulo(/Cartão$/), { target: { value: cartao.id } })
  fireEvent.change(campoPorRotulo(/Parcelas/), { target: { value: '10' } })
  fireEvent.change(campoPorRotulo('Data da 1ª parcela'), { target: { value: '2026-11-10' } })
  fireEvent.change(campoPorRotulo('Valor de cada parcela'), { target: { value: '199,16' } })
  fireEvent.change(campoPorRotulo('Banco da fatura'), { target: { value: 'Nubank' } })

  expect(await screen.findByText(
    '10× de R$ 199,16 = R$ 1.991,60 · 1ª em 10/11 na fatura Nubank · as outras 9 ficam previstas em Contas a Pagar até 10/08/2027',
  )).toBeInTheDocument()
})

// Item 5 (Money Vision): "Entrada (da reserva)" pré-preenchida com o saldo real disponível.
test('Financiado com conta de investimento vinculada mostra "Entrada (da reserva)" pré-preenchida', () => {
  const meta = criarMeta({ contaInvestimentoId: 'invest-1', valorSonho: 30000 })
  render(<ConquistarMetaModal meta={meta} contasBancarias={[criarContaInvestimento(), criarContaCartao({ id: 'conta-financ' })]} onClose={vi.fn()} onConquistada={vi.fn()} />)

  irParaForma('Financiado')

  expect(screen.getByText(/Entrada \(da reserva\)/)).toBeInTheDocument()
  expect(campoPorRotulo(/Entrada \(da reserva\)/)).toHaveValue('5.000,00')
})

test('confirmar com Financiado navega pra Financiamentos levando a entrada e o resgate', async () => {
  const meta = criarMeta({ contaInvestimentoId: 'invest-1', valorSonho: 30000 })
  const contaFinanc = { ...criarContaCartao({ id: 'conta-financ' }), tipo: 'ContaCorrente' as const }
  render(<ConquistarMetaModal meta={meta} contasBancarias={[criarContaInvestimento(), contaFinanc]} onClose={vi.fn()} onConquistada={vi.fn()} />)

  irParaForma('Financiado')
  fireEvent.change(campoPorRotulo(/Conta do financiamento/), { target: { value: contaFinanc.id } })

  fireEvent.click(screen.getByRole('button', { name: 'Abrir cadastro de financiamento' }))

  await waitFor(() => expect(mockNavigate).toHaveBeenCalledWith('/contas/financiamentos', {
    state: {
      novoFinanciamento: {
        descricao: 'Carro', valorBem: 30000, contaBancariaId: contaFinanc.id, origemMetaId: meta.id,
        valorEntrada: 5000, resgatarEntradaDoInvestimento: true,
      },
    },
  }))
})

test('confirmar "lançar agora" no cartão envia valor da parcela, banco, total e lembra as parcelas', async () => {
  const cartao = criarContaCartao()
  vi.mocked(metasApi.conquistarMeta).mockResolvedValue(criarMeta({ status: 'Conquistada' }))
  // iconeSonho pré-preenche a categoria (via CATEGORIA_POR_ICONE) — sem categoria, confirmar()
  // bloqueia antes de chamar a API, o que não é o que este teste quer exercitar.
  render(<ConquistarMetaModal meta={criarMeta({ iconeSonho: '💻' })} contasBancarias={[cartao]} onClose={vi.fn()} onConquistada={vi.fn()} />)

  irParaCartaoLancandoAgora()
  fireEvent.change(campoPorRotulo(/Cartão$/), { target: { value: cartao.id } })
  fireEvent.change(campoPorRotulo(/Parcelas/), { target: { value: '8' } })
  fireEvent.change(campoPorRotulo('Data da 1ª parcela'), { target: { value: '2026-11-10' } })
  fireEvent.change(campoPorRotulo('Valor de cada parcela'), { target: { value: '500,00' } })
  fireEvent.change(campoPorRotulo('Banco da fatura'), { target: { value: 'Nubank' } })

  fireEvent.click(screen.getByRole('button', { name: '🏆 Confirmar conquista' }))

  await waitFor(() => expect(metasApi.conquistarMeta).toHaveBeenCalledWith('meta-1', expect.objectContaining({
    modo: 'LancarAgora', parcelas: 8, dataPrimeiraParcela: '2026-11-10', valorParcela: 500, valorPago: 4000, banco: 'Nubank',
  })))
  expect(localStorage.getItem('conquistarMeta.parcelasCartao')).toBe('8')
})

test('"lançar agora" no cartão sem banco não chama a API', async () => {
  const cartao = criarContaCartao()
  render(<ConquistarMetaModal meta={criarMeta({ iconeSonho: '💻' })} contasBancarias={[cartao]} onClose={vi.fn()} onConquistada={vi.fn()} />)

  irParaCartaoLancandoAgora()
  fireEvent.change(campoPorRotulo(/Cartão$/), { target: { value: cartao.id } })
  fireEvent.change(campoPorRotulo('Valor de cada parcela'), { target: { value: '500,00' } })
  fireEvent.click(screen.getByRole('button', { name: '🏆 Confirmar conquista' }))

  expect(await screen.findByText('Informe o banco da fatura (ex.: Nubank, C6).')).toBeInTheDocument()
  expect(metasApi.conquistarMeta).not.toHaveBeenCalled()
})

// ── "Já lancei essa compra / ela vai vir na fatura" ──────────────────────────────────────────

const candidatosTrocafone = {
  parcelamentos: [],
  lancamentos: [
    { id: 'l-nu', contaBancariaId: 'cartao-1', contaNome: 'Cartões', contaEhCartao: true, banco: 'Nubank', data: '2026-10-05', descricao: 'Trocafone - Parcela 1/10', valor: 199.16, parcelamentoId: 'p-nu', numeroParcela: 1, totalParcelas: 10 },
    { id: 'l-c6', contaBancariaId: 'cartao-1', contaNome: 'Cartões', contaEhCartao: true, banco: 'C6', data: '2026-10-06', descricao: 'TROCAFONE', valor: 210 },
    { id: 'l-mercado', contaBancariaId: 'cc-1', contaNome: 'Itaú', contaEhCartao: false, data: '2026-10-07', descricao: 'Mercado', valor: 300 },
  ],
}

test('Cartão abre em "Já lancei" e lista as compras pra vincular, com busca', async () => {
  vi.mocked(metasApi.listarCandidatosVinculoMeta).mockResolvedValue({
    ...candidatosTrocafone,
    parcelamentos: [{ id: 'p-nu', descricao: 'Trocafone', banco: 'Nubank', contaNome: 'Cartões', valorParcela: 199.16, totalParcelas: 10, parcelasPagas: 1, valorTotal: 1991.6 }],
  })
  render(<ConquistarMetaModal meta={criarMeta()} contasBancarias={[criarContaCartao()]} onClose={vi.fn()} onConquistada={vi.fn()} />)

  irParaForma('Cartão')

  expect(await screen.findByRole('checkbox', { name: /Trocafone · Nubank · 1 de 10 · 10× R\$\s199,16/ })).toBeInTheDocument()
  expect(screen.getByRole('checkbox', { name: /Mercado/ })).toBeInTheDocument()
  expect(screen.queryByText('Valor de cada parcela', { selector: 'label' })).not.toBeInTheDocument()

  fireEvent.change(screen.getByRole('searchbox'), { target: { value: 'trocafone' } })
  expect(screen.queryByRole('checkbox', { name: /Mercado/ })).not.toBeInTheDocument()
})

test('confirmar "Já lancei" com os dois Trocafone só vincula, sem lançar nada', async () => {
  vi.mocked(metasApi.listarCandidatosVinculoMeta).mockResolvedValue(candidatosTrocafone)
  vi.mocked(metasApi.conquistarMeta).mockResolvedValue(criarMeta({ status: 'Conquistada' }))
  const onConquistada = vi.fn()
  render(<ConquistarMetaModal meta={criarMeta()} contasBancarias={[criarContaCartao()]} onClose={vi.fn()} onConquistada={onConquistada} />)

  irParaForma('Cartão')
  fireEvent.click(await screen.findByRole('checkbox', { name: /Trocafone - Parcela 1\/10 · Nubank/ }))
  fireEvent.click(screen.getByRole('checkbox', { name: /TROCAFONE · C6/ }))
  // Lançamento solto do C6: a detecção não reconheceu — o usuário diz que é a parcela 1 de 10.
  fireEvent.change(screen.getByLabelText('Número da parcela de TROCAFONE'), { target: { value: '1' } })
  fireEvent.change(screen.getByLabelText('Total de parcelas de TROCAFONE'), { target: { value: '10' } })

  expect(screen.getByText(/Nenhum lançamento novo será criado/)).toBeInTheDocument()
  fireEvent.click(screen.getByRole('button', { name: '🏆 Confirmar conquista' }))

  await waitFor(() => expect(metasApi.conquistarMeta).toHaveBeenCalledWith('meta-1', expect.objectContaining({
    modo: 'JaLancado', formaPagamento: 'Cartao', parcelamentoIds: [],
    lancamentos: [
      { lancamentoId: 'l-nu', contaBancariaId: 'cartao-1', data: '2026-10-05', numeroParcela: undefined, totalParcelas: undefined },
      { lancamentoId: 'l-c6', contaBancariaId: 'cartao-1', data: '2026-10-06', numeroParcela: 1, totalParcelas: 10 },
    ],
  })))
  expect(onConquistada).toHaveBeenCalled()
})

test('"Já lancei" sem nada marcado não chama a API', async () => {
  vi.mocked(metasApi.listarCandidatosVinculoMeta).mockResolvedValue(candidatosTrocafone)
  render(<ConquistarMetaModal meta={criarMeta()} contasBancarias={[criarContaCartao()]} onClose={vi.fn()} onConquistada={vi.fn()} />)

  irParaForma('Cartão')
  await screen.findByRole('checkbox', { name: /Mercado/ })
  fireEvent.click(screen.getByRole('button', { name: '🏆 Confirmar conquista' }))

  expect(await screen.findByText('Marque o parcelamento ou o lançamento da compra.')).toBeInTheDocument()
  expect(metasApi.conquistarMeta).not.toHaveBeenCalled()
})
