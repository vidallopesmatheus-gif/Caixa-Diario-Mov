// frontend/src/pages/client/ClientCaixaPage.test.tsx
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react'
import ClientCaixaPage from './ClientCaixaPage'
import * as AuthContextModule from '../../contexts/AuthContext'
import * as useRegistrosHook from '../../hooks/useRegistros'
import * as contasBancariasApi from '../../api/contasBancarias'
import * as categoriasApi from '../../api/categorias'
import * as importacaoApi from '../../api/importacao'
import { todayISO } from '../../utils/format'
import type { ContaBancaria } from '../../types'

vi.mock('../../contexts/AuthContext', async (importOriginal) => {
  const actual = await importOriginal<typeof AuthContextModule>()
  return { ...actual, useAuth: vi.fn() }
})
vi.mock('../../hooks/useRegistros')
vi.mock('../../api/contasBancarias', async (importOriginal) => {
  const actual = await importOriginal<typeof contasBancariasApi>()
  return { ...actual, listarContasBancarias: vi.fn() }
})
vi.mock('../../api/categorias', async (importOriginal) => {
  const actual = await importOriginal<typeof categoriasApi>()
  return { ...actual, listarCategorias: vi.fn() }
})
vi.mock('../../api/importacao', async (importOriginal) => {
  const actual = await importOriginal<typeof importacaoApi>()
  return { ...actual, excluirLancamento: vi.fn() }
})

const mockUser = { usuarioId: 'u1', nomeUsuario: 'cli1', perfil: 'cliente' as const, nomeCompleto: 'Cliente Um', nomeEstabelecimento: '', token: 'tok' }

const contaCaixaPadrao: ContaBancaria = {
  id: 'conta1', clienteId: 'u1', nome: 'Caixa', tipo: 'Caixa',
  saldoInicial: 0, saldoAtual: 0, entradasMes: 0, saidasMes: 0,
  pendentesCategorizacao: 0, ativa: true, dataCriacao: '2026-01-01',
}

function mockHooks(overrides: Partial<ReturnType<typeof useRegistrosHook.useRegistros>> = {}) {
  vi.mocked(AuthContextModule.useAuth).mockReturnValue({
    user: mockUser,
    login: vi.fn(),
    logout: vi.fn(),
  })
  vi.mocked(useRegistrosHook.useRegistros).mockReturnValue({
    registros: [],
    loading: false,
    erro: '',
    salvar: vi.fn().mockResolvedValue({}),
    excluir: vi.fn(),
    buscarPorData: vi.fn().mockResolvedValue(null),
    recarregar: vi.fn(),
    ...overrides,
  } as ReturnType<typeof useRegistrosHook.useRegistros>)
  vi.mocked(contasBancariasApi.listarContasBancarias).mockResolvedValue([contaCaixaPadrao])
  vi.mocked(categoriasApi.listarCategorias).mockResolvedValue({ entradas: [], saidas: [] })
}

// Item 2.3: a tela agora espera contas + registro-do-dia carregarem antes de sair do skeleton —
// todo teste precisa esperar esse carregamento terminar antes de consultar o formulário real.
async function renderCaixa() {
  render(<ClientCaixaPage />)
  await screen.findByText('📋 Registro do dia')
}

test('renderiza campos do formulário de caixa', async () => {
  mockHooks()
  await renderCaixa()
  expect(screen.getAllByPlaceholderText('0,00').length).toBeGreaterThan(0)
  expect(screen.getByText(/Salvar e sincronizar/)).toBeInTheDocument()
})

test('exibe StatCards com labels corretos', async () => {
  mockHooks()
  await renderCaixa()
  expect(screen.getByText('📥 Início')).toBeInTheDocument()
  expect(screen.getByText('📤 Entradas')).toBeInTheDocument()
  expect(screen.getByText('💸 Saídas')).toBeInTheDocument()
  expect(screen.getByText('💰 Saldo')).toBeInTheDocument()
})

test('exibe mensagem de sucesso após salvar', async () => {
  const salvar = vi.fn().mockResolvedValue({})
  mockHooks({ salvar })
  await renderCaixa()
  fireEvent.click(screen.getByText(/Salvar e sincronizar/))
  await waitFor(() => expect(screen.getByText(/Salvo com sucesso/)).toBeInTheDocument())
})

test('exibe mensagem de erro quando salvar falha', async () => {
  const salvar = vi.fn().mockRejectedValue(new Error('Falha de rede'))
  mockHooks({ salvar })
  await renderCaixa()
  fireEvent.click(screen.getByText(/Salvar e sincronizar/))
  await waitFor(() => expect(screen.getByText(/Falha de rede/)).toBeInTheDocument())
})

test('botão fica desabilitado durante salvamento', async () => {
  const salvar = vi.fn().mockImplementation(() => new Promise(() => {}))
  mockHooks({ salvar })
  await renderCaixa()
  fireEvent.click(screen.getByText(/Salvar e sincronizar/))
  await waitFor(() => expect(screen.getByText('Salvando...')).toBeDisabled())
})

test('adiciona nova linha de saída ao clicar em Adicionar saída', async () => {
  mockHooks()
  await renderCaixa()
  const antes = screen.getAllByPlaceholderText('Descrição').length
  fireEvent.click(screen.getByText(/Adicionar saída/))
  expect(screen.getAllByPlaceholderText('Descrição')).toHaveLength(antes + 1)
})

test('botões de navegação de dia estão presentes', async () => {
  mockHooks()
  await renderCaixa()
  expect(screen.getByText('←')).toBeInTheDocument()
  expect(screen.getByText('→')).toBeInTheDocument()
})

test('navega para o dia anterior ao clicar em ←', async () => {
  mockHooks()
  await renderCaixa()
  fireEvent.click(screen.getByText('←'))
  expect(screen.getByText('←')).toBeInTheDocument()
})

test('navega para o dia seguinte ao clicar em →', async () => {
  mockHooks()
  await renderCaixa()
  fireEvent.click(screen.getByText('→'))
  expect(screen.getByText('→')).toBeInTheDocument()
})

test('remove linha de saída ao clicar em ✕', async () => {
  mockHooks()
  await renderCaixa()
  // adiciona uma saída extra primeiro
  fireEvent.click(screen.getByText(/Adicionar saída/))
  const botoesRemover = screen.getAllByLabelText('Remover lançamento')
  const contaAntes = screen.getAllByPlaceholderText('Descrição').length
  fireEvent.click(botoesRemover[0])
  expect(screen.getAllByPlaceholderText('Descrição')).toHaveLength(contaAntes - 1)
})

test('não exibe seções de contas a receber e contas a pagar', async () => {
  mockHooks()
  await renderCaixa()
  expect(screen.queryByText(/Adicionar a Receber/)).not.toBeInTheDocument()
  expect(screen.queryByText(/Adicionar a Pagar/)).not.toBeInTheDocument()
})

test('exibe diferença de saldo quando confirmado é preenchido', async () => {
  mockHooks()
  await renderCaixa()
  const inputs = screen.getAllByPlaceholderText('0,00')
  // o último input é o de confirmar saldo
  fireEvent.change(inputs[inputs.length - 1], { target: { value: '999' } })
  // alguma mensagem de diferença ou conferido deve aparecer
  const dif = document.querySelector('.dif-msg')
  expect(dif).toBeInTheDocument()
})

test('carrega dados do registro existente quando buscarPorData retorna resultado', async () => {
  const reg = {
    id: 'r1', clienteId: 'u1', data: '2026-05-15',
    saldoInicio: 500, entradas: [{ descricao: 'Caixa', valor: 200 }],
    saidas: [{ descricao: 'Aluguel', valor: 100, categoria: 'Administrativas', subcategoria: '' }],
    contasAReceber: [{ descricao: 'Mensalidade', valor: 300, pago: false }],
    contasAPagar: [{ descricao: 'Fornecedor', valor: 50, pago: false }],
    saldoConfirmado: 600, saldoCalculado: 600, criadoEm: '',
  }
  const buscarPorData = vi.fn().mockResolvedValue(reg)
  mockHooks({ buscarPorData })
  await renderCaixa()
  await waitFor(() => expect(buscarPorData).toHaveBeenCalled())
})

test('carrega saldo anterior quando buscarPorData retorna null e há registros anteriores', async () => {
  const registros = [{
    id: 'r0', clienteId: 'u1', data: '2026-04-01',
    saldoInicio: 100, entradas: [], saidas: [], contasAReceber: [], contasAPagar: [],
    saldoConfirmado: 300, saldoCalculado: 300, criadoEm: '',
  }]
  const buscarPorData = vi.fn().mockResolvedValue(null)
  mockHooks({ buscarPorData, registros })
  await renderCaixa()
  await waitFor(() => expect(buscarPorData).toHaveBeenCalled())
})

test('usa o saldoInicio já calculado pelo backend quando não há registro no dia (Fase 1.13)', async () => {
  // Desde a Fase 1.13 o backend sempre devolve 200 com o saldoInicio já calculado (último
  // registro real anterior desta conta) mesmo quando o dia em si não tem registro persistido —
  // a tela não precisa (e não deve) procurar isso sozinha na lista de registros.
  const GUID_VAZIO = '00000000-0000-0000-0000-000000000000'
  const buscarPorData = vi.fn().mockResolvedValue({
    id: GUID_VAZIO, clienteId: 'u1', data: todayISO(),
    saldoInicio: 1200, entradas: [], saidas: [], contasAReceber: [], contasAPagar: [],
    saldoConfirmado: 1200, saldoCalculado: 1200, criadoEm: '',
  })
  mockHooks({ buscarPorData })
  await renderCaixa()

  const inicioCard = screen.getByText('📥 Início').closest('.stat-card') as HTMLElement
  await waitFor(() => expect(inicioCard).toHaveTextContent('1.200,00'))
})

test('não salva quando clienteId é nulo', async () => {
  vi.mocked(AuthContextModule.useAuth).mockReturnValue({ user: null, login: vi.fn(), logout: vi.fn() })
  vi.mocked(useRegistrosHook.useRegistros).mockReturnValue({
    registros: [], loading: false, erro: '',
    salvar: vi.fn(), excluir: vi.fn(), buscarPorData: vi.fn().mockResolvedValue(null), recarregar: vi.fn(),
  } as ReturnType<typeof useRegistrosHook.useRegistros>)
  await renderCaixa()
  fireEvent.click(screen.getByText(/Salvar e sincronizar/))
  await waitFor(() => expect(screen.queryByText(/Salvo com sucesso/)).not.toBeInTheDocument())
})

test('atualiza campo de descrição de saída', async () => {
  mockHooks()
  await renderCaixa()
  const descInputs = screen.getAllByPlaceholderText('Descrição')
  fireEvent.change(descInputs[0], { target: { value: 'Nova despesa' } })
  expect((descInputs[0] as HTMLInputElement).value).toBe('Nova despesa')
})

test('atualiza campo de valor de saída', async () => {
  mockHooks()
  await renderCaixa()
  // escopa à seção de Saídas — o valor de Entradas também usa o placeholder "0,00"
  const saidasSection = screen.getByText(/Saídas do dia/).closest('.inp-group') as HTMLElement
  const valorInput = within(saidasSection).getAllByPlaceholderText('0,00')[0]
  fireEvent.change(valorInput, { target: { value: '150' } })
  expect((valorInput as HTMLInputElement).value).toBe('150')
})

test('salvar sem categoria em saída exibe mensagem e não chama a API', async () => {
  const salvar = vi.fn().mockResolvedValue({})
  mockHooks({ salvar })
  await renderCaixa()
  // escopa a query à seção de Saídas para evitar colisão com campos de Entradas
  const saidasSection = screen.getByText(/Saídas do dia/).closest('.inp-group') as HTMLElement
  const saidasContainer = within(saidasSection)
  // preenche descrição e valor da primeira linha de saída para que ela passe no filtro (descricao || valor)
  fireEvent.change(saidasContainer.getByPlaceholderText('Descrição'), { target: { value: 'Aluguel' } })
  fireEvent.change(saidasContainer.getAllByPlaceholderText('0,00')[0], { target: { value: '100' } })
  // não seleciona categoria — clica em salvar
  fireEvent.click(screen.getByText(/Salvar e sincronizar/))
  await waitFor(() =>
    expect(screen.getByText('Selecione uma categoria para cada saída.')).toBeInTheDocument()
  )
  expect(salvar).not.toHaveBeenCalled()
})

// ── Item 2.1: botões de ação (🔁/✔/✕) acessíveis e sem sobreposição ──────────────────────────
test('botões de confirmar e remover têm aria-label e não se sobrepõem (mesmo pai .lanc-acoes)', async () => {
  mockHooks()
  await renderCaixa()
  const confirmar = screen.getAllByLabelText('Confirmar lançamento')[0]
  const remover = screen.getAllByLabelText('Remover lançamento')[0]
  expect(confirmar).toBeInTheDocument()
  expect(remover).toBeInTheDocument()
  expect(confirmar.parentElement).toBe(remover.parentElement)
  expect(confirmar.parentElement).toHaveClass('lanc-acoes')
})

// ── Item 2.4: editar/excluir item já salvo direto no Caixa ───────────────────────────────────
test('editar um item salvo devolve ele pra linha de rascunho e remove o selo', async () => {
  const reg = {
    id: 'r1', clienteId: 'u1', data: todayISO(),
    saldoInicio: 0, entradas: [{ id: 'e1', descricao: 'Venda', valor: 200 }],
    saidas: [], contasAReceber: [], contasAPagar: [],
    saldoConfirmado: 0, saldoCalculado: 0, criadoEm: '',
  }
  mockHooks({ buscarPorData: vi.fn().mockResolvedValue(reg) })
  await renderCaixa()
  await screen.findByText(/Venda · R\$ 200,00/)

  fireEvent.click(screen.getByLabelText('Editar Venda'))

  expect(screen.queryByText(/Venda · R\$ 200,00/)).not.toBeInTheDocument()
  // o item editado volta como a 1ª linha de rascunho de Entradas
  const entradasSection = screen.getByText('💵 Entradas do dia').closest('.inp-group') as HTMLElement
  expect(within(entradasSection).getAllByPlaceholderText('Descrição')[0]).toHaveValue('Venda')
})

test('excluir um item salvo chama excluirLancamento e remove o selo', async () => {
  const reg = {
    id: 'r1', clienteId: 'u1', data: todayISO(),
    saldoInicio: 0, entradas: [{ id: 'e1', descricao: 'Venda', valor: 200 }],
    saidas: [], contasAReceber: [], contasAPagar: [],
    saldoConfirmado: 0, saldoCalculado: 0, criadoEm: '',
  }
  mockHooks({ buscarPorData: vi.fn().mockResolvedValue(reg) })
  vi.spyOn(importacaoApi, 'excluirLancamento').mockResolvedValue({ transferenciaExcluida: false, tituloReaberto: null })
  vi.spyOn(window, 'confirm').mockReturnValue(true)
  await renderCaixa()
  await screen.findByText(/Venda · R\$ 200,00/)

  fireEvent.click(screen.getByLabelText('Excluir Venda'))

  await waitFor(() => expect(screen.queryByText(/Venda · R\$ 200,00/)).not.toBeInTheDocument())
  expect(importacaoApi.excluirLancamento).toHaveBeenCalledWith('conta1', { id: 'e1', data: todayISO() })
})
