import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import ClientContaDetalhePage from './ClientContaDetalhePage'
import * as AuthContextModule from '../../contexts/AuthContext'
import * as contasBancariasApi from '../../api/contasBancarias'
import * as categoriasApi from '../../api/categorias'
import * as importacaoApi from '../../api/importacao'
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
  vi.mocked(AuthContextModule.useAuth).mockReturnValue({ user: mockUser, login: vi.fn(), logout: vi.fn() } as never)
  vi.mocked(contasBancariasApi.listarContasBancarias).mockResolvedValue([conta])
  vi.mocked(contasBancariasApi.obterPendenciasConta).mockResolvedValue({ recebiveis: [], pagamentos: [] })
  vi.mocked(categoriasApi.listarCategorias).mockResolvedValue(categorias)
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
