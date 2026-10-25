import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import ClientContaDetalhePage from './ClientContaDetalhePage'
import * as AuthContextModule from '../../contexts/AuthContext'
import * as contasBancariasApi from '../../api/contasBancarias'
import * as categoriasApi from '../../api/categorias'
import * as importacaoApi from '../../api/importacao'
import * as regrasApi from '../../api/regras'
import * as transferenciasApi from '../../api/transferencias'
import * as faturasCartaoApi from '../../api/faturasCartao'
import type { ContaBancaria, LancamentoExtrato, Categorias, ContaProvisionada, ResumoImportacao } from '../../types'
import { addDays, todayISO } from '../../utils/format'

vi.mock('../../contexts/AuthContext', async (importOriginal) => {
  const actual = await importOriginal<typeof AuthContextModule>()
  return { ...actual, useAuth: vi.fn() }
})
vi.mock('../../api/contasBancarias')
vi.mock('../../api/categorias')
vi.mock('../../api/importacao')
vi.mock('../../api/transferencias')
vi.mock('../../api/conciliacao')
vi.mock('../../api/contasProvisionadas')
vi.mock('../../api/regras')
vi.mock('../../api/metas')
vi.mock('../../api/faturasCartao')

const mockUser = { usuarioId: 'cliente-1', nomeUsuario: 'cli1', perfil: 'cliente' as const, nomeCompleto: 'C', nomeEstabelecimento: '', token: 'tok' }

const conta: ContaBancaria = {
  id: 'conta-1', clienteId: 'cliente-1', nome: 'Nubank', tipo: 'ContaCorrente',
  saldoInicial: 0, saldoAtual: 132.66, entradasMes: 0, saidasMes: 0, pendentesCategorizacao: 0,
  ativa: true, dataCriacao: '2026-01-01',
}

const contaInvestimento: ContaBancaria = {
  id: 'conta-1', clienteId: 'cliente-1', nome: 'Nubank Investimento', tipo: 'Investimento',
  saldoInicial: 0, saldoAtual: 4046.68, entradasMes: 0, saidasMes: 0, pendentesCategorizacao: 0,
  ativa: true, dataCriacao: '2026-01-01',
  totalAportado: 4000, rendimentoAcumulado: 46.68, rentabilidadePercentual: 1.17,
  aporteMesAtual: 1300, resgateMesAtual: 0, aporteLiquidoMesAtual: 1300, rendimentoMesAtual: 20,
  rentabilidadeMesAtualPercentual: 0.5, rentabilidadeAnoAtualPercentual: 1.17,
  ultimaAtualizacaoRendimento: '2026-10-01',
  metasVinculadas: [{ id: 'meta-1', ano: 2027, sonho: 'Carro novo', valorSonho: 10000, previsaoAtingimentoMesAno: 'mar/2027' }],
  progressoCombinadoPercentual: 40.5,
}

const contaCartao: ContaBancaria = {
  id: 'conta-1', clienteId: 'cliente-1', nome: 'Cartões', tipo: 'CartaoCredito',
  saldoInicial: 0, saldoAtual: -500, entradasMes: 0, saidasMes: 0, pendentesCategorizacao: 0,
  ativa: true, dataCriacao: '2026-01-01',
}

const categorias: Categorias = {
  entradas: [{ nome: 'Vendas', tipoCusto: 'Receita' }],
  saidas: [{ nome: 'Aluguel', tipoCusto: 'CustoFixo' }, { nome: 'Rendimentos', tipoCusto: 'Investimento' }],
}

function lancamento(overrides: Partial<LancamentoExtrato>): LancamentoExtrato {
  return {
    id: 'item-1', data: '2026-10-08', descricao: 'Compra no débito - PADARIA CASA MOSTRA',
    valor: -50, saldoAcumulado: 100, pendenteCategorizacao: false, categoria: 'Aluguel',
    ...overrides,
  }
}

function renderPagina() {
  return render(
    <MemoryRouter initialEntries={['/banco/conta-1']}>
      <Routes>
        <Route path="/banco/:contaId" element={<ClientContaDetalhePage />} />
      </Routes>
    </MemoryRouter>,
  )
}

beforeEach(() => {
  // Sem isso, chamadas de mock de um teste anterior (ex.: excluirLancamento) continuam contando
  // pros toHaveBeenCalledTimes() do teste seguinte, já que vi.mock() não zera entre testes.
  vi.clearAllMocks()
  vi.mocked(AuthContextModule.useAuth).mockReturnValue({ user: mockUser, login: vi.fn(), logout: vi.fn() } as never)
  vi.mocked(contasBancariasApi.listarContasBancarias).mockResolvedValue([conta])
  vi.mocked(contasBancariasApi.obterPendenciasConta).mockResolvedValue({ recebiveis: [], pagamentos: [] })
  vi.mocked(categoriasApi.listarCategorias).mockResolvedValue(categorias)
  vi.mocked(regrasApi.contarCorrespondencias).mockResolvedValue(0)
  vi.mocked(transferenciasApi.listarTransferencias).mockResolvedValue([])
  vi.mocked(contasBancariasApi.obterHistoricoSaldoMensal).mockResolvedValue([])
})

test('agrupa lançamentos por dia com entradas, saídas e saldo do dia no cabeçalho', async () => {
  vi.mocked(contasBancariasApi.obterExtratoConta).mockResolvedValue([
    lancamento({ id: 'a', descricao: 'Venda', valor: 300, saldoAcumulado: 432.66, categoria: 'Vendas' }),
    lancamento({ id: 'b', descricao: 'Aluguel', valor: -200, saldoAcumulado: 132.66, categoria: 'Aluguel' }),
  ])
  renderPagina()

  const cabecalho = await screen.findByText('08/10/2026')
  const grupo = cabecalho.closest('.cd-extrato-dia-cabecalho') as HTMLElement
  expect(within(grupo).getByText(/Entradas \+R\$ 300,00/)).toBeInTheDocument()
  expect(within(grupo).getByText(/Saídas −R\$ 200,00/)).toBeInTheDocument()
  expect(within(grupo).getByText(/Saldo do dia R\$ 432,66/)).toBeInTheDocument()
})

test('descrição bruta de débito aparece limpa e capitalizada no título da linha', async () => {
  vi.mocked(contasBancariasApi.obterExtratoConta).mockResolvedValue([
    lancamento({ descricao: 'Compra no débito - PADARIA CASA MOSTRA' }),
  ])
  renderPagina()

  // O título limpo aparece duas vezes no DOM (layout desktop em grid + cards mobile — CSS
  // decide qual mostrar via media query, que o jsdom não aplica), então usa getAllByText.
  const titulos = await screen.findAllByText('Débito · Padaria Casa Mostra')
  expect(titulos.length).toBeGreaterThan(0)
})

test('chip de categoria usa a cor de investimento quando o tipo da categoria é Investimento', async () => {
  vi.mocked(contasBancariasApi.obterExtratoConta).mockResolvedValue([
    lancamento({ categoria: 'Rendimentos', valor: 50 }),
  ])
  renderPagina()

  const chip = await screen.findByRole('button', { name: 'Rendimentos' })
  expect(chip.className).toContain('cd-chip-investimento')
})

test('lançamento sem categoria usa o chip laranja "Sem categoria"', async () => {
  vi.mocked(contasBancariasApi.obterExtratoConta).mockResolvedValue([
    lancamento({ categoria: undefined, pendenteCategorizacao: true }),
  ])
  renderPagina()

  const chip = await screen.findByRole('button', { name: 'Sem categoria' })
  expect(chip.className).toContain('cd-chip-sem-categoria')
})

test('excluir abre modal de confirmação com botão vermelho, não um confirm() nativo', async () => {
  vi.mocked(contasBancariasApi.obterExtratoConta).mockResolvedValue([lancamento({})])
  const confirmSpy = vi.spyOn(window, 'confirm')
  const user = userEvent.setup()
  renderPagina()

  await user.click(await screen.findByRole('button', { name: /Excluir lançamento/ }))

  expect(await screen.findByText(/Excluir "Compra no débito/)).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Excluir' })).toHaveClass('btn-danger')
  expect(confirmSpy).not.toHaveBeenCalled()
})

test('confirmar exclusão chama a API e fecha o modal', async () => {
  vi.mocked(contasBancariasApi.obterExtratoConta).mockResolvedValue([lancamento({})])
  vi.mocked(importacaoApi.excluirLancamento).mockResolvedValue({ transferenciaExcluida: false, tituloReaberto: null })
  const user = userEvent.setup()
  renderPagina()

  await user.click(await screen.findByRole('button', { name: /Excluir lançamento/ }))
  await user.click(await screen.findByRole('button', { name: 'Excluir' }))

  await waitFor(() => expect(importacaoApi.excluirLancamento).toHaveBeenCalledWith('conta-1', { id: 'item-1', data: '2026-10-08' }))
  await waitFor(() => expect(screen.queryByText(/Excluir "Compra no débito/)).not.toBeInTheDocument())
})

// ── Bloco 2: filtros, seleção em lote e "aplicar a todos e criar regra?" ────────────────────

test('filtro de texto esconde lançamentos cuja descrição não casa com a busca', async () => {
  vi.mocked(contasBancariasApi.obterExtratoConta).mockResolvedValue([
    lancamento({ id: 'a', descricao: 'Compra no débito - Uber', data: '2026-10-08' }),
    lancamento({ id: 'b', descricao: 'Compra no débito - Farmácia', data: '2026-10-07', saldoAcumulado: 50 }),
  ])
  const user = userEvent.setup()
  renderPagina()

  await screen.findAllByText('Débito · Uber')
  await user.type(screen.getByPlaceholderText('🔎 Buscar na descrição...'), 'uber')

  expect(screen.getAllByText('Débito · Uber').length).toBeGreaterThan(0)
  expect(screen.queryByText('Débito · Farmácia')).not.toBeInTheDocument()
})

test('filtro "Só sem categoria" mostra só os lançamentos pendentes', async () => {
  vi.mocked(contasBancariasApi.obterExtratoConta).mockResolvedValue([
    lancamento({ id: 'a', descricao: 'Compra no débito - Uber', categoria: 'Aluguel' }),
    lancamento({ id: 'b', descricao: 'Compra no débito - Farmácia', categoria: undefined, pendenteCategorizacao: true }),
  ])
  const user = userEvent.setup()
  renderPagina()

  await screen.findAllByText('Débito · Uber')
  await user.click(screen.getByLabelText('Só sem categoria'))

  expect(screen.queryByText('Débito · Uber')).not.toBeInTheDocument()
  expect(screen.getAllByText('Débito · Farmácia').length).toBeGreaterThan(0)
})

test('selecionar lançamentos mostra a barra de lote e excluir em lote chama a API pra cada um', async () => {
  vi.mocked(contasBancariasApi.obterExtratoConta).mockResolvedValue([
    lancamento({ id: 'a', descricao: 'Compra no débito - Uber', data: '2026-10-08' }),
    lancamento({ id: 'b', descricao: 'Compra no débito - Farmácia', data: '2026-10-07', saldoAcumulado: 50 }),
  ])
  vi.mocked(importacaoApi.excluirLancamento).mockResolvedValue({ transferenciaExcluida: false, tituloReaberto: null })
  const user = userEvent.setup()
  renderPagina()

  // Cada lançamento tem um checkbox no layout desktop E outro no card mobile (CSS escolhe qual
  // mostrar) — filtra só os de dentro de ".cd-extrato-linha" (desktop) pra não contar 2x, e cada
  // dia tem seu próprio ".cd-extrato-lista", então não dá pra escopar num só.
  await screen.findAllByText('Débito · Uber')
  const checkboxes = screen.getAllByLabelText(/Selecionar/).filter(el => el.closest('.cd-extrato-linha'))
  expect(checkboxes).toHaveLength(2)
  await user.click(checkboxes[0])
  await user.click(checkboxes[1])

  expect(await screen.findByText('2 selecionado(s)')).toBeInTheDocument()

  await user.click(screen.getByRole('button', { name: 'Excluir' }))
  // Depois de abrir o modal, "Excluir" aparece 2x (botão da barra de lote + botão vermelho de
  // confirmação) — escopa pelo texto do modal pra achar o botão certo.
  const aviso = await screen.findByText('Excluir 2 lançamento(s) selecionado(s)? Essa ação não pode ser desfeita.')
  const modal = aviso.closest('.modal') as HTMLElement
  await user.click(within(modal).getByRole('button', { name: 'Excluir' }))

  await waitFor(() => expect(importacaoApi.excluirLancamento).toHaveBeenCalledTimes(2))
})

test('categorizar via chip oferece aplicar a todos e criar regra quando há lançamentos parecidos', async () => {
  vi.mocked(contasBancariasApi.obterExtratoConta).mockResolvedValue([
    lancamento({ categoria: undefined, pendenteCategorizacao: true }),
  ])
  vi.mocked(regrasApi.contarCorrespondencias).mockResolvedValue(3)
  vi.mocked(regrasApi.criarRegra).mockResolvedValue({
    id: 'regra-1', contaBancariaId: 'conta-1', contaBancariaNome: 'Nubank', tipo: 'Saida',
    descricaoReferencia: 'x', criterioTipo: 'DescricaoExata', criterioValor: 'x', acaoTipo: 'Categoria',
    categoria: 'Aluguel', ativa: true, sugerida: false, ordem: 0, quantidadeAplicada: 0, criadoEm: '2026-10-08',
  })
  vi.mocked(regrasApi.aplicarRegraRetroativamente).mockResolvedValue(3)
  const user = userEvent.setup()
  renderPagina()

  await user.click(await screen.findByRole('button', { name: 'Sem categoria' }))
  await user.type(await screen.findByPlaceholderText('Categoria...'), 'Aluguel')
  // Escopa pro listbox do combobox — a barra de filtros também tem um <option> "Aluguel" (do
  // <select> de categoria), que a árvore de acessibilidade também expõe com role="option".
  const combobox = document.querySelector('.cd-chip-combobox') as HTMLElement
  await user.click(await within(combobox).findByRole('option', { name: 'Aluguel' }))

  expect(await screen.findByText(/Encontrei 3 outro\(s\) lançamento\(s\) parecido\(s\)/)).toBeInTheDocument()

  await user.click(screen.getByRole('button', { name: 'Aplicar e criar regra' }))

  await waitFor(() => expect(regrasApi.criarRegra).toHaveBeenCalledWith('cliente-1', expect.objectContaining({
    contaBancariaId: 'conta-1', tipo: 'Saida', acaoTipo: 'Categoria', categoria: 'Aluguel',
  })))
  await waitFor(() => expect(regrasApi.aplicarRegraRetroativamente).toHaveBeenCalledWith('regra-1'))
})

// ── Bloco 3: Ajuste de Saldo ──────────────────────────────────────────────────────────────────

test('lançamento com tipoCusto AjusteSaldo mostra o chip cinza "Ajuste de saldo", mesmo categorizado', async () => {
  vi.mocked(contasBancariasApi.obterExtratoConta).mockResolvedValue([
    lancamento({ descricao: 'saldo', categoria: 'Devoluções', tipoCusto: 'AjusteSaldo' }),
  ])
  renderPagina()

  const chip = await screen.findByRole('button', { name: 'Ajuste de saldo' })
  expect(chip.className).toContain('cd-chip-ajuste')
  expect(chip).toBeDisabled()
})

test('ajuste de saldo desabilita editar categoria e marcar transferência, mas não excluir', async () => {
  vi.mocked(contasBancariasApi.obterExtratoConta).mockResolvedValue([
    lancamento({ descricao: 'saldo', categoria: 'Devoluções', tipoCusto: 'AjusteSaldo' }),
  ])
  renderPagina()

  expect(await screen.findByRole('button', { name: /Editar categoria/ })).toBeDisabled()
  expect(screen.getByRole('button', { name: /Marcar .* como transferência/ })).toBeDisabled()
  expect(screen.getByRole('button', { name: /Excluir lançamento/ })).toBeEnabled()
})

test('ajuste de saldo não entra no subtotal de Entradas/Saídas do dia, mas o saldo do dia é o real', async () => {
  vi.mocked(contasBancariasApi.obterExtratoConta).mockResolvedValue([
    // Lista vem do backend da mais recente pra mais antiga do dia — o ajuste é o último evento
    // do dia, por isso aparece primeiro, e o saldo do dia é o dele (132.66, não 432.66).
    lancamento({ id: 'b', descricao: 'saldo', categoria: 'Devoluções', tipoCusto: 'AjusteSaldo', valor: -3895.76, saldoAcumulado: 132.66 }),
    lancamento({ id: 'a', descricao: 'Venda', categoria: 'Vendas', valor: 300, saldoAcumulado: 432.66 }),
  ])
  renderPagina()

  const cabecalho = (await screen.findByText('08/10/2026')).closest('.cd-extrato-dia-cabecalho') as HTMLElement
  expect(within(cabecalho).getByText(/Entradas \+R\$ 300,00/)).toBeInTheDocument()
  expect(within(cabecalho).queryByText(/Saídas/)).not.toBeInTheDocument()
  expect(within(cabecalho).getByText(/Saldo do dia R\$ 132,66/)).toBeInTheDocument()
})

// ── Bloco 4: chip de transferência mostra a outra conta ─────────────────────────────────────────

test('chip de transferência mostra "⇄ {outra conta}" em vez do literal "Transferência"', async () => {
  vi.mocked(contasBancariasApi.obterExtratoConta).mockResolvedValue([
    lancamento({ categoria: 'Transferência', transferenciaId: 'transf-1' }),
  ])
  vi.mocked(transferenciasApi.listarTransferencias).mockResolvedValue([{
    id: 'transf-1', clienteId: 'cliente-1', contaOrigemId: 'conta-1', contaOrigemNome: 'Nubank',
    contaDestinoId: 'conta-investimento', contaDestinoNome: 'Nubank Investimento', data: '2026-10-08', valor: 50, criadoEm: '2026-10-08',
  }])
  renderPagina()

  const chip = await screen.findByRole('button', { name: '⇄ Nubank Investimento' })
  expect(chip.className).toContain('cd-chip-transferencia')
})

// ── Bloco 5: conta de investimento — resumo do mês, meta com previsão, "Atualizar saldo" ───────

test('conta de investimento mostra o resumo do mês e a previsão de atingimento da meta', async () => {
  vi.mocked(contasBancariasApi.listarContasBancarias).mockResolvedValue([contaInvestimento])
  vi.mocked(contasBancariasApi.obterExtratoConta).mockResolvedValue([])
  renderPagina()

  const cardAportes = (await screen.findByText('Aportes')).closest('.cb-resumo-item') as HTMLElement
  expect(within(cardAportes).getByText('R$ 1.300,00')).toBeInTheDocument()
  const cardRendimento = screen.getByText('Rendimento do mês').closest('.cb-resumo-item') as HTMLElement
  expect(within(cardRendimento).getByText('R$ 20,00')).toBeInTheDocument()
  expect(screen.getByText('Última atualização: 01/10/2026')).toBeInTheDocument()
  expect(screen.getByText(/No seu ritmo de aporte \(média dos últimos 3 meses\) você chega lá em mar\/2027/)).toBeInTheDocument()
})

test('"Atualizar saldo" no modo padrão calcula a diferença e lança como rendimento', async () => {
  vi.mocked(contasBancariasApi.listarContasBancarias).mockResolvedValue([contaInvestimento])
  vi.mocked(contasBancariasApi.obterExtratoConta).mockResolvedValue([])
  vi.mocked(contasBancariasApi.registrarRendimento).mockResolvedValue(contaInvestimento)
  const user = userEvent.setup()
  renderPagina()

  await user.click(await screen.findByRole('button', { name: '📈 Atualizar saldo' }))
  const grupoSaldo = (await screen.findByText('Saldo atual no app do banco')).closest('.inp-group') as HTMLElement
  await user.type(within(grupoSaldo).getByPlaceholderText('0,00'), '4100')

  expect(await screen.findByText(/Rendimento de R\$ 53,32 a lançar/)).toBeInTheDocument()

  await user.click(screen.getByRole('button', { name: 'Confirmar' }))

  await waitFor(() => expect(contasBancariasApi.registrarRendimento).toHaveBeenCalledWith('conta-1', expect.objectContaining({
    valor: expect.closeTo(53.32, 2),
  })))
})

test('"Atualizar saldo" com diferença negativa usa a descrição "Ajuste negativo"', async () => {
  vi.mocked(contasBancariasApi.listarContasBancarias).mockResolvedValue([contaInvestimento])
  vi.mocked(contasBancariasApi.obterExtratoConta).mockResolvedValue([])
  vi.mocked(contasBancariasApi.registrarRendimento).mockResolvedValue(contaInvestimento)
  const user = userEvent.setup()
  renderPagina()

  await user.click(await screen.findByRole('button', { name: '📈 Atualizar saldo' }))
  const grupoSaldo = (await screen.findByText('Saldo atual no app do banco')).closest('.inp-group') as HTMLElement
  await user.type(within(grupoSaldo).getByPlaceholderText('0,00'), '4000')

  expect(await screen.findByText(/Ajuste negativo de R\$ 46,68/)).toBeInTheDocument()

  await user.click(screen.getByRole('button', { name: 'Confirmar' }))

  await waitFor(() => expect(contasBancariasApi.registrarRendimento).toHaveBeenCalledWith('conta-1', expect.objectContaining({
    valor: expect.closeTo(-46.68, 2), descricao: 'Ajuste negativo (IR/IOF/oscilação)',
  })))
})

// ── Parte 3 (Money Vision): seletor de mês nas abas Recebíveis/Pagamentos ──────────────────────

function pendencia(overrides: Partial<ContaProvisionada>): ContaProvisionada {
  return { descricao: 'Pendência', valor: 100, pago: false, ...overrides }
}

test('pendência atrasada some da aba no modo "mês" e aparece no aviso e no modo "Atrasadas"', async () => {
  vi.mocked(contasBancariasApi.obterPendenciasConta).mockResolvedValue({
    recebiveis: [],
    pagamentos: [pendencia({ descricao: 'Fornecedor atrasado', valor: 500, dataVencimento: addDays(todayISO(), -10) })],
  })
  const user = userEvent.setup()
  renderPagina()

  await user.click(await screen.findByRole('button', { name: /Pagamentos/ }))
  expect(await screen.findByText(/1 pendência\(s\) atrasada\(s\) · R\$ 500,00/)).toBeInTheDocument()
  expect(screen.queryByText('Fornecedor atrasado')).not.toBeInTheDocument()

  await user.click(screen.getByRole('button', { name: 'Atrasadas' }))
  expect(await screen.findByText('Fornecedor atrasado')).toBeInTheDocument()
  expect(screen.getByText(/atrasada há 10 dias/)).toBeInTheDocument()
})

test('recebível com vencimento no mês atual aparece na aba Recebíveis sem precisar trocar o modo', async () => {
  vi.mocked(contasBancariasApi.obterPendenciasConta).mockResolvedValue({
    recebiveis: [pendencia({ descricao: 'Cliente do mês', valor: 250, dataVencimento: todayISO() })],
    pagamentos: [],
  })
  const user = userEvent.setup()
  renderPagina()

  await user.click(await screen.findByRole('button', { name: /Recebíveis/ }))
  expect(await screen.findByText('Cliente do mês')).toBeInTheDocument()
})

// ── Cartão (Bloco 2, Money Vision): importação de fatura ───────────────────────────────────────

function mockResumoImportacao(overrides: Partial<ResumoImportacao> = {}): ResumoImportacao {
  return {
    totalEncontradas: 2, totalJaImportadas: 0, totalNovas: 2, totalEntradas: 0, totalSaidas: 150,
    quantidadeEntradas: 0, quantidadeSaidas: 2, sinalInvertidoParaCartao: false,
    dataInicioArquivo: '2026-10-01', dataFimArquivo: '2026-10-05',
    duplicatasManuais: [], duplicatasEntreArquivos: [],
    ...overrides,
  }
}

async function selecionarArquivo(arquivo: File) {
  // O input fica escondido até a conta terminar de carregar — espera o botão "Importar extrato"
  // aparecer antes de procurar o input, senão o querySelector roda contra a tela de loading.
  await screen.findByRole('button', { name: /Importar extrato/ })
  const input = document.querySelector('input[type="file"]') as HTMLInputElement
  await userEvent.setup().upload(input, arquivo)
}

test('conta Cartão de crédito: rótulo mostra "Cartão de crédito" em vez do tipo cru', async () => {
  vi.mocked(contasBancariasApi.listarContasBancarias).mockResolvedValue([contaCartao])
  vi.mocked(contasBancariasApi.obterExtratoConta).mockResolvedValue([])

  renderPagina()

  expect(await screen.findByText('💳 Cartão de crédito')).toBeInTheDocument()
  expect(screen.queryByText('CartaoCredito')).not.toBeInTheDocument()
})

test('conta Cartão de crédito: modal de importação exige banco e vencimento antes de liberar o botão', async () => {
  vi.mocked(contasBancariasApi.listarContasBancarias).mockResolvedValue([contaCartao])
  vi.mocked(contasBancariasApi.obterExtratoConta).mockResolvedValue([])
  vi.mocked(importacaoApi.previewExtrato).mockResolvedValue(mockResumoImportacao())
  vi.mocked(faturasCartaoApi.listarBancosCartao).mockResolvedValue(['Nubank', 'C6'])
  const user = userEvent.setup()
  renderPagina()

  const arquivo = new File(['conteudo'], 'fatura.csv', { type: 'text/csv' })
  await selecionarArquivo(arquivo)

  const btnImportar = await screen.findByRole('button', { name: /Importar 2 transação/ })
  expect(btnImportar).toBeDisabled()

  await user.type(screen.getByPlaceholderText('Ex.: Nubank'), 'Nubank')
  expect(btnImportar).toBeDisabled() // falta o vencimento

  const grupoVencimento = screen.getByText('Vencimento').closest('.inp-group') as HTMLElement
  await user.type(within(grupoVencimento).getByDisplayValue(''), '2026-11-10')

  expect(btnImportar).not.toBeDisabled()
})

test('conta Cartão de crédito: confirmar importação envia banco, vencimento e competência sugerida', async () => {
  vi.mocked(contasBancariasApi.listarContasBancarias).mockResolvedValue([contaCartao])
  vi.mocked(contasBancariasApi.obterExtratoConta).mockResolvedValue([])
  vi.mocked(importacaoApi.previewExtrato).mockResolvedValue(mockResumoImportacao())
  vi.mocked(faturasCartaoApi.listarBancosCartao).mockResolvedValue([])
  vi.mocked(importacaoApi.importarExtrato).mockResolvedValue({
    totalImportadas: 2, totalPendentesCategorizacao: 2, totalCategorizadasPorRegra: 0,
    totalConciliadasTransferencia: 0, totalAmbiguasTransferencia: 0, totalEntradas: 0, totalSaidas: 150,
    totalMescladasComManual: 0, totalDuplicatasEntreArquivosSinalizadas: 0, totalDuplicatasEntreArquivosIgnoradas: 0,
    totalSugestoesVinculo: 0, faturaId: 'fatura-1', parcelamentosDetectados: [],
  })
  const user = userEvent.setup()
  renderPagina()

  const arquivo = new File(['conteudo'], 'fatura.csv', { type: 'text/csv' })
  await selecionarArquivo(arquivo)

  await user.type(await screen.findByPlaceholderText('Ex.: Nubank'), 'Nubank')
  const grupoVencimento = screen.getByText('Vencimento').closest('.inp-group') as HTMLElement
  await user.type(within(grupoVencimento).getByDisplayValue(''), '2026-11-10')

  await user.click(screen.getByRole('button', { name: /Importar 2 transação/ }))

  await waitFor(() => expect(importacaoApi.importarExtrato).toHaveBeenCalledWith('conta-1', arquivo, expect.objectContaining({
    banco: 'Nubank',
    dataVencimentoFatura: '2026-11-10',
    competenciaFatura: '2026-11',
  })))
})

test('conta ContaCorrente: modal de importação não pede campos de fatura', async () => {
  vi.mocked(contasBancariasApi.obterExtratoConta).mockResolvedValue([])
  vi.mocked(importacaoApi.previewExtrato).mockResolvedValue(mockResumoImportacao())
  renderPagina()

  const arquivo = new File(['conteudo'], 'extrato.csv', { type: 'text/csv' })
  await selecionarArquivo(arquivo)

  await screen.findByRole('button', { name: /Importar 2 transação/ })
  expect(screen.queryByText('Cartão / banco')).not.toBeInTheDocument()
})
