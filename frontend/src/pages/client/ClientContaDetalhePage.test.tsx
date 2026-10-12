import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import ClientContaDetalhePage from './ClientContaDetalhePage'
import * as AuthContextModule from '../../contexts/AuthContext'
import * as contasBancariasApi from '../../api/contasBancarias'
import * as categoriasApi from '../../api/categorias'
import * as importacaoApi from '../../api/importacao'
import * as regrasApi from '../../api/regras'
import type { ContaBancaria, LancamentoExtrato, Categorias } from '../../types'

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

const mockUser = { usuarioId: 'cliente-1', nomeUsuario: 'cli1', perfil: 'cliente' as const, nomeCompleto: 'C', nomeEstabelecimento: '', token: 'tok' }

const conta: ContaBancaria = {
  id: 'conta-1', clienteId: 'cliente-1', nome: 'Nubank', tipo: 'ContaCorrente',
  saldoInicial: 0, saldoAtual: 132.66, entradasMes: 0, saidasMes: 0, pendentesCategorizacao: 0,
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
