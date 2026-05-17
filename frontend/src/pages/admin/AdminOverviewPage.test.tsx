// frontend/src/pages/admin/AdminOverviewPage.test.tsx
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import AdminOverviewPage from './AdminOverviewPage'
import * as useUsuariosHook from '../../hooks/useUsuarios'
import * as useRegistrosHook from '../../hooks/useRegistros'
import type { Usuario } from '../../types'

vi.mock('../../hooks/useUsuarios')
vi.mock('../../hooks/useRegistros')

const mockClientes: Usuario[] = [
  { id: 'u1', nomeUsuario: 'cli1', nomeCompleto: 'Cliente Um', nomeEstabelecimento: 'Loja Um', perfil: 'cliente', ativo: true, criadoEm: '', criadoPor: '' },
  { id: 'u2', nomeUsuario: 'cli2', nomeCompleto: 'Cliente Dois', nomeEstabelecimento: 'Loja Dois', perfil: 'cliente', ativo: true, criadoEm: '', criadoPor: '' },
]

function mockHooks(usuariosOverride: Partial<ReturnType<typeof useUsuariosHook.useUsuarios>> = {}) {
  vi.mocked(useUsuariosHook.useUsuarios).mockReturnValue({
    usuarios: mockClientes,
    loading: false,
    erro: '',
    criar: vi.fn(),
    atualizar: vi.fn(),
    desativar: vi.fn(),
    recarregar: vi.fn(),
    ...usuariosOverride,
  } as ReturnType<typeof useUsuariosHook.useUsuarios>)

  vi.mocked(useRegistrosHook.useRegistros).mockReturnValue({
    registros: [],
    loading: false,
    erro: '',
    salvar: vi.fn(),
    excluir: vi.fn(),
    buscarPorData: vi.fn(),
    recarregar: vi.fn(),
  } as ReturnType<typeof useRegistrosHook.useRegistros>)
}

function renderPage() {
  return render(<MemoryRouter><AdminOverviewPage /></MemoryRouter>)
}

test('exibe loading enquanto carrega', () => {
  mockHooks({ loading: true, usuarios: [] })
  renderPage()
  expect(screen.getByText(/Carregando/)).toBeInTheDocument()
})

test('renderiza StatCard com contagem de clientes ativos', () => {
  mockHooks()
  renderPage()
  expect(screen.getByText('2')).toBeInTheDocument()
})

test('renderiza um card para cada cliente ativo', () => {
  mockHooks()
  renderPage()
  expect(screen.getByText('Cliente Um')).toBeInTheDocument()
  expect(screen.getByText('Cliente Dois')).toBeInTheDocument()
})

test('exibe nome do estabelecimento em cada card', () => {
  mockHooks()
  renderPage()
  expect(screen.getByText('Loja Um')).toBeInTheDocument()
  expect(screen.getByText('Loja Dois')).toBeInTheDocument()
})

test('não renderiza clientes inativos', () => {
  const comInativo = [
    ...mockClientes,
    { id: 'u3', nomeUsuario: 'inat', nomeCompleto: 'Inativo', nomeEstabelecimento: '', perfil: 'cliente' as const, ativo: false, criadoEm: '', criadoPor: '' },
  ]
  mockHooks({ usuarios: comInativo })
  renderPage()
  expect(screen.queryByText('Inativo')).not.toBeInTheDocument()
})
