// frontend/src/pages/admin/AdminClientsPage.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import AdminClientsPage from './AdminClientsPage'
import * as useUsuariosHook from '../../hooks/useUsuarios'
import type { Usuario } from '../../types'

vi.mock('../../hooks/useUsuarios')

const mockClientes: Usuario[] = [
  { id: 'u1', nomeUsuario: 'cli1', nomeCompleto: 'Cliente Um', nomeEstabelecimento: 'Loja Um', perfil: 'cliente', ativo: true, criadoEm: '', criadoPor: '' },
  { id: 'u2', nomeUsuario: 'cli2', nomeCompleto: 'Cliente Dois', nomeEstabelecimento: 'Loja Dois', perfil: 'cliente', ativo: true, criadoEm: '', criadoPor: '' },
]

function mockHook(overrides: Partial<ReturnType<typeof useUsuariosHook.useUsuarios>> = {}) {
  vi.mocked(useUsuariosHook.useUsuarios).mockReturnValue({
    usuarios: mockClientes,
    loading: false,
    erro: '',
    criar: vi.fn().mockResolvedValue({}),
    atualizar: vi.fn().mockResolvedValue({}),
    desativar: vi.fn().mockResolvedValue(undefined),
    recarregar: vi.fn(),
    ...overrides,
  } as ReturnType<typeof useUsuariosHook.useUsuarios>)
}

test('exibe loading enquanto carrega', () => {
  mockHook({ loading: true, usuarios: [] })
  render(<AdminClientsPage />)
  expect(screen.getByText(/Carregando/)).toBeInTheDocument()
})

test('renderiza lista de clientes filtrados por perfil cliente', () => {
  mockHook()
  render(<AdminClientsPage />)
  expect(screen.getByText('Cliente Um')).toBeInTheDocument()
  expect(screen.getByText('Cliente Dois')).toBeInTheDocument()
})

test('exibe contagem de clientes no painel', () => {
  mockHook()
  render(<AdminClientsPage />)
  expect(screen.getByText(/2 clientes/)).toBeInTheDocument()
})

test('abre modal ao clicar em Novo cliente', () => {
  mockHook()
  render(<AdminClientsPage />)
  fireEvent.click(screen.getByText(/Novo cliente/))
  expect(screen.getByText('Novo cliente', { selector: 'h2, h3, div' })).toBeInTheDocument()
})

test('exibe mensagem de erro ao submeter modal vazio', async () => {
  mockHook()
  render(<AdminClientsPage />)
  fireEvent.click(screen.getByText(/Novo cliente/))
  const form = document.querySelector('.modal form')!
  fireEvent.submit(form)
  await waitFor(() =>
    expect(screen.getByText(/Preencha todos os campos/)).toBeInTheDocument()
  )
})

test('exibe painel de edição ao clicar em cliente', () => {
  mockHook()
  render(<AdminClientsPage />)
  fireEvent.click(screen.getByText('Cliente Um'))
  expect(screen.getByText('Excluir')).toBeInTheDocument()
})

test('abre modal de confirmação ao clicar em Excluir', () => {
  mockHook()
  render(<AdminClientsPage />)
  fireEvent.click(screen.getByText('Cliente Um'))
  fireEvent.click(screen.getByText('Excluir'))
  expect(screen.getByText(/Confirmar exclusão/)).toBeInTheDocument()
})

test('chama desativar ao confirmar exclusão', async () => {
  const desativar = vi.fn().mockResolvedValue(undefined)
  mockHook({ desativar })
  render(<AdminClientsPage />)
  fireEvent.click(screen.getByText('Cliente Um'))
  fireEvent.click(screen.getByText('Excluir'))
  const botoesExcluir = screen.getAllByText('Excluir')
  fireEvent.click(botoesExcluir[botoesExcluir.length - 1])
  await waitFor(() => expect(desativar).toHaveBeenCalledWith('u1'))
})

test('exibe erro quando criar falha', async () => {
  const criar = vi.fn().mockRejectedValue(new Error('Usuário já existe'))
  mockHook({ criar })
  render(<AdminClientsPage />)
  fireEvent.click(screen.getByText(/Novo cliente/))
  const inputs = screen.getAllByRole('textbox')
  fireEvent.change(inputs[0], { target: { value: 'Novo Cliente' } })
  fireEvent.change(inputs[2], { target: { value: 'novouser' } })
  const senhaInput = document.querySelector('input[type="password"]')!
  fireEvent.change(senhaInput, { target: { value: '1234' } })
  fireEvent.click(screen.getByText('Salvar'))
  await waitFor(() =>
    expect(screen.getByText('Usuário já existe')).toBeInTheDocument()
  )
})
