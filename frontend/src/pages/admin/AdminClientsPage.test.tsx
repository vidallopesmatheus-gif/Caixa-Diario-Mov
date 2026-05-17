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
  expect(screen.getByLabelText(/Nome completo/)).toBeInTheDocument()
})

test('exibe mensagem de erro ao submeter modal vazio', async () => {
  mockHook()
  render(<AdminClientsPage />)
  fireEvent.click(screen.getByText(/Novo cliente/))
  const salvarBtn = screen.getByRole('button', { name: 'Salvar' })
  fireEvent.submit(salvarBtn.closest('form')!)
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
  fireEvent.change(screen.getByLabelText(/Nome completo/), { target: { value: 'Novo Cliente' } })
  fireEvent.change(screen.getByLabelText(/Usuário/), { target: { value: 'novouser' } })
  const senhaInput = document.querySelector('input[type="password"]')!
  fireEvent.change(senhaInput, { target: { value: '1234' } })
  const salvarBtn = screen.getByRole('button', { name: 'Salvar' })
  fireEvent.submit(salvarBtn.closest('form')!)
  await waitFor(() =>
    expect(screen.getByText('Usuário já existe')).toBeInTheDocument()
  )
})

test('exibe formulário de edição ao selecionar cliente (sem campo Usuário)', () => {
  mockHook()
  render(<AdminClientsPage />)
  fireEvent.click(screen.getByText('Cliente Um'))
  // no edit mode, Usuário field should not be present
  expect(screen.queryByLabelText(/Usuário/)).not.toBeInTheDocument()
  expect(screen.getByLabelText(/Nome completo/)).toBeInTheDocument()
})

test('chama atualizar ao salvar formulário de edição', async () => {
  const atualizar = vi.fn().mockResolvedValue({})
  mockHook({ atualizar })
  render(<AdminClientsPage />)
  fireEvent.click(screen.getByText('Cliente Um'))
  const nomeInput = screen.getByLabelText(/Nome completo/)
  fireEvent.change(nomeInput, { target: { value: 'Cliente Atualizado' } })
  const salvarBtn = screen.getByRole('button', { name: 'Salvar' })
  fireEvent.submit(salvarBtn.closest('form')!)
  await waitFor(() => expect(atualizar).toHaveBeenCalledWith('u1', expect.objectContaining({ nomeCompleto: 'Cliente Atualizado' })))
})

test('exibe erro quando atualizar falha', async () => {
  const atualizar = vi.fn().mockRejectedValue(new Error('Falha ao atualizar'))
  mockHook({ atualizar })
  render(<AdminClientsPage />)
  fireEvent.click(screen.getByText('Cliente Um'))
  const salvarBtn = screen.getByRole('button', { name: 'Salvar' })
  fireEvent.submit(salvarBtn.closest('form')!)
  await waitFor(() => expect(screen.getByText('Falha ao atualizar')).toBeInTheDocument())
})

test('exibe erro quando desativar falha', async () => {
  const desativar = vi.fn().mockRejectedValue(new Error('Falha ao desativar'))
  mockHook({ desativar })
  render(<AdminClientsPage />)
  fireEvent.click(screen.getByText('Cliente Um'))
  fireEvent.click(screen.getByText('Excluir'))
  const botoesExcluir = screen.getAllByText('Excluir')
  fireEvent.click(botoesExcluir[botoesExcluir.length - 1])
  await waitFor(() => expect(screen.getByText('Falha ao desativar')).toBeInTheDocument())
})

test('fecha modal de adição ao clicar em Cancelar', () => {
  mockHook()
  render(<AdminClientsPage />)
  fireEvent.click(screen.getByText(/Novo cliente/))
  expect(screen.getByLabelText(/Nome completo/)).toBeInTheDocument()
  fireEvent.click(screen.getByRole('button', { name: 'Cancelar' }))
  expect(screen.queryByLabelText(/Usuário/)).not.toBeInTheDocument()
})

test('cancela edição ao clicar em Cancelar no formulário de edição', () => {
  mockHook()
  render(<AdminClientsPage />)
  fireEvent.click(screen.getByText('Cliente Um'))
  expect(screen.getByLabelText(/Nome completo/)).toBeInTheDocument()
  fireEvent.click(screen.getByRole('button', { name: 'Cancelar' }))
  expect(screen.getByText(/Selecione um cliente/)).toBeInTheDocument()
})

test('fecha modal de exclusão ao clicar em Cancelar', () => {
  mockHook()
  render(<AdminClientsPage />)
  fireEvent.click(screen.getByText('Cliente Um'))
  fireEvent.click(screen.getByText('Excluir'))
  expect(screen.getByText(/Confirmar exclusão/)).toBeInTheDocument()
  // há dois botões Cancelar: um no formulário de edição e um no modal de exclusão
  const cancelarBtns = screen.getAllByRole('button', { name: 'Cancelar' })
  fireEvent.click(cancelarBtns[cancelarBtns.length - 1])
  expect(screen.queryByText(/Confirmar exclusão/)).not.toBeInTheDocument()
})

test('exibe indicador de inativo para cliente inativo', () => {
  const comInativo = [
    ...mockClientes,
    { id: 'u3', nomeUsuario: 'inat', nomeCompleto: 'Cliente Inativo', nomeEstabelecimento: 'Loja Inativa', perfil: 'cliente' as const, ativo: false, criadoEm: '', criadoPor: '' },
  ]
  mockHook({ usuarios: comInativo })
  render(<AdminClientsPage />)
  expect(screen.getByText('Inativo')).toBeInTheDocument()
})
