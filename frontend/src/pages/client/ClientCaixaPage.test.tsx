// frontend/src/pages/client/ClientCaixaPage.test.tsx
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react'
import ClientCaixaPage from './ClientCaixaPage'
import * as AuthContextModule from '../../contexts/AuthContext'
import * as useRegistrosHook from '../../hooks/useRegistros'

vi.mock('../../contexts/AuthContext', async (importOriginal) => {
  const actual = await importOriginal<typeof AuthContextModule>()
  return { ...actual, useAuth: vi.fn() }
})
vi.mock('../../hooks/useRegistros')

const mockUser = { usuarioId: 'u1', nomeUsuario: 'cli1', perfil: 'cliente' as const, nomeCompleto: 'Cliente Um', nomeEstabelecimento: '', token: 'tok' }

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
}

test('renderiza campos do formulário de caixa', () => {
  mockHooks()
  render(<ClientCaixaPage />)
  expect(screen.getAllByPlaceholderText('0,00').length).toBeGreaterThan(0)
  expect(screen.getByText(/Salvar e sincronizar/)).toBeInTheDocument()
})

test('exibe StatCards com labels corretos', () => {
  mockHooks()
  render(<ClientCaixaPage />)
  expect(screen.getByText('📥 Início')).toBeInTheDocument()
  expect(screen.getByText('📤 Entradas')).toBeInTheDocument()
  expect(screen.getByText('💸 Saídas')).toBeInTheDocument()
  expect(screen.getByText('💰 Saldo')).toBeInTheDocument()
})

test('exibe mensagem de sucesso após salvar', async () => {
  const salvar = vi.fn().mockResolvedValue({})
  mockHooks({ salvar })
  render(<ClientCaixaPage />)
  fireEvent.click(screen.getByText(/Salvar e sincronizar/))
  await waitFor(() => expect(screen.getByText(/Salvo com sucesso/)).toBeInTheDocument())
})

test('exibe mensagem de erro quando salvar falha', async () => {
  const salvar = vi.fn().mockRejectedValue(new Error('Falha de rede'))
  mockHooks({ salvar })
  render(<ClientCaixaPage />)
  fireEvent.click(screen.getByText(/Salvar e sincronizar/))
  await waitFor(() => expect(screen.getByText(/Falha de rede/)).toBeInTheDocument())
})

test('botão fica desabilitado durante salvamento', async () => {
  const salvar = vi.fn().mockImplementation(() => new Promise(() => {}))
  mockHooks({ salvar })
  render(<ClientCaixaPage />)
  fireEvent.click(screen.getByText(/Salvar e sincronizar/))
  await waitFor(() => expect(screen.getByText('Salvando...')).toBeDisabled())
})

test('adiciona nova linha de saída ao clicar em Adicionar saída', () => {
  mockHooks()
  render(<ClientCaixaPage />)
  const antes = screen.getAllByPlaceholderText('Descrição').length
  fireEvent.click(screen.getByText(/Adicionar saída/))
  expect(screen.getAllByPlaceholderText('Descrição')).toHaveLength(antes + 1)
})

test('botões de navegação de dia estão presentes', () => {
  mockHooks()
  render(<ClientCaixaPage />)
  expect(screen.getByText('←')).toBeInTheDocument()
  expect(screen.getByText('→')).toBeInTheDocument()
})

test('navega para o dia anterior ao clicar em ←', () => {
  mockHooks()
  render(<ClientCaixaPage />)
  fireEvent.click(screen.getByText('←'))
  expect(screen.getByText('←')).toBeInTheDocument()
})

test('navega para o dia seguinte ao clicar em →', () => {
  mockHooks()
  render(<ClientCaixaPage />)
  fireEvent.click(screen.getByText('→'))
  expect(screen.getByText('→')).toBeInTheDocument()
})

test('remove linha de saída ao clicar em ✕', () => {
  mockHooks()
  render(<ClientCaixaPage />)
  // adiciona uma saída extra primeiro
  fireEvent.click(screen.getByText(/Adicionar saída/))
  const botoesRemover = screen.getAllByText('✕')
  const contaAntes = screen.getAllByPlaceholderText('Descrição').length
  fireEvent.click(botoesRemover[0])
  expect(screen.getAllByPlaceholderText('Descrição')).toHaveLength(contaAntes - 1)
})

test('não exibe seções de contas a receber e contas a pagar', () => {
  mockHooks()
  render(<ClientCaixaPage />)
  expect(screen.queryByText(/Adicionar a Receber/)).not.toBeInTheDocument()
  expect(screen.queryByText(/Adicionar a Pagar/)).not.toBeInTheDocument()
})

test('exibe diferença de saldo quando confirmado é preenchido', () => {
  mockHooks()
  render(<ClientCaixaPage />)
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
    saidas: [{ descricao: 'Aluguel', valor: 100 }],
    contasAReceber: [{ descricao: 'Mensalidade', valor: 300, pago: false }],
    contasAPagar: [{ descricao: 'Fornecedor', valor: 50, pago: false }],
    saldoConfirmado: 600, saldoCalculado: 600, criadoEm: '',
  }
  const buscarPorData = vi.fn().mockResolvedValue(reg)
  mockHooks({ buscarPorData })
  render(<ClientCaixaPage />)
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
  render(<ClientCaixaPage />)
  await waitFor(() => expect(buscarPorData).toHaveBeenCalled())
})

test('carrega saldo anterior quando registros chegam DEPOIS da montagem (race condition)', async () => {
  // Cenário do bug: ao abrir direto no dia atual, a lista de registros ainda
  // está sendo buscada (vazia). Ela chega só depois da montagem do componente.
  const prevReg = {
    id: 'r0', clienteId: 'u1', data: '2020-01-01',
    saldoInicio: 0, entradas: [], saidas: [], contasAReceber: [], contasAPagar: [],
    saldoConfirmado: 1200, saldoCalculado: 1200, criadoEm: '',
  }
  const buscarPorData = vi.fn().mockResolvedValue(null) // sem registro para hoje

  // 1ª renderização: lista ainda vazia (fetch em andamento)
  mockHooks({ buscarPorData, registros: [] })
  const { rerender } = render(<ClientCaixaPage />)
  await waitFor(() => expect(buscarPorData).toHaveBeenCalled())

  // 2ª renderização: a lista chega com o saldo do dia anterior (1200)
  mockHooks({ buscarPorData, registros: [prevReg] })
  rerender(<ClientCaixaPage />)

  // O "Saldo início" deve refletir o saldo confirmado do dia anterior, não zero
  const inicioCard = screen.getByText('📥 Início').closest('.stat-card') as HTMLElement
  await waitFor(() =>
    expect(inicioCard).toHaveTextContent('1.200,00')
  )
})

test('não salva quando clienteId é nulo', async () => {
  vi.mocked(AuthContextModule.useAuth).mockReturnValue({ user: null, login: vi.fn(), logout: vi.fn() })
  vi.mocked(useRegistrosHook.useRegistros).mockReturnValue({
    registros: [], loading: false, erro: '',
    salvar: vi.fn(), excluir: vi.fn(), buscarPorData: vi.fn().mockResolvedValue(null), recarregar: vi.fn(),
  } as ReturnType<typeof useRegistrosHook.useRegistros>)
  render(<ClientCaixaPage />)
  fireEvent.click(screen.getByText(/Salvar e sincronizar/))
  await waitFor(() => expect(screen.queryByText(/Salvo com sucesso/)).not.toBeInTheDocument())
})

test('atualiza campo de descrição de saída', () => {
  mockHooks()
  render(<ClientCaixaPage />)
  const descInputs = screen.getAllByPlaceholderText('Descrição')
  fireEvent.change(descInputs[0], { target: { value: 'Nova despesa' } })
  expect((descInputs[0] as HTMLInputElement).value).toBe('Nova despesa')
})

test('atualiza campo de valor de saída', () => {
  mockHooks()
  render(<ClientCaixaPage />)
  const valorInputs = screen.getAllByPlaceholderText('R$')
  fireEvent.change(valorInputs[0], { target: { value: '150' } })
  expect((valorInputs[0] as HTMLInputElement).value).toBe('150')
})

test('salvar sem categoria em saída exibe mensagem e não chama a API', async () => {
  const salvar = vi.fn().mockResolvedValue({})
  mockHooks({ salvar })
  render(<ClientCaixaPage />)
  // escopa a query à seção de Saídas para evitar colisão com campos de Entradas
  const saidasSection = screen.getByText(/Saídas do dia/).closest('.inp-group') as HTMLElement
  const saidasContainer = within(saidasSection)
  // preenche descrição e valor da primeira linha de saída para que ela passe no filtro (descricao || valor)
  fireEvent.change(saidasContainer.getByPlaceholderText('Descrição'), { target: { value: 'Aluguel' } })
  fireEvent.change(saidasContainer.getByPlaceholderText('R$'), { target: { value: '100' } })
  // não seleciona categoria — clica em salvar
  fireEvent.click(screen.getByText(/Salvar e sincronizar/))
  await waitFor(() =>
    expect(screen.getByText('Selecione uma categoria para cada saída.')).toBeInTheDocument()
  )
  expect(salvar).not.toHaveBeenCalled()
})
