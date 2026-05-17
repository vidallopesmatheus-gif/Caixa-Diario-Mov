import { login } from './auth'
import { listarUsuarios, criarUsuario, atualizarUsuario, desativarUsuario } from './usuarios'

beforeEach(() => {
  vi.stubGlobal('fetch', vi.fn())
  vi.mocked(fetch).mockResolvedValue({
    ok: true,
    status: 200,
    json: async () => ({ dados: { token: 'jwt', nome: 'CTI', perfil: 'admin', id: '1' } }),
  } as Response)
})

afterEach(() => vi.unstubAllGlobals())

test('login faz POST em /api/auth/login', async () => {
  await login('CTI', 'senha123')
  expect(vi.mocked(fetch)).toHaveBeenCalledWith(
    '/api/auth/login',
    expect.objectContaining({ method: 'POST' })
  )
})

test('login envia nomeUsuario e senha no body JSON', async () => {
  await login('CTI', 'senha123')
  const call = vi.mocked(fetch).mock.calls[0][1] as RequestInit
  expect(JSON.parse(call.body as string)).toEqual({ nomeUsuario: 'CTI', senha: 'senha123' })
})

// ─── usuarios.ts ────────────────────────────────────────────────────────────

test('listarUsuarios faz GET em /api/usuarios', async () => {
  vi.mocked(fetch).mockResolvedValue({ ok: true, status: 200, json: async () => ({ dados: [] }) } as Response)
  await listarUsuarios()
  expect(vi.mocked(fetch)).toHaveBeenCalledWith('/api/usuarios', expect.any(Object))
})

test('criarUsuario faz POST em /api/usuarios com body correto', async () => {
  vi.mocked(fetch).mockResolvedValue({ ok: true, status: 200, json: async () => ({ dados: {} }) } as Response)
  const dto = { nomeUsuario: 'u1', senha: '123', nomeCompleto: 'User', nomeEstabelecimento: 'Loja', perfil: 'cliente' }
  await criarUsuario(dto)
  const call = vi.mocked(fetch).mock.calls[0][1] as RequestInit
  expect(call.method).toBe('POST')
  expect(JSON.parse(call.body as string)).toMatchObject(dto)
})

test('atualizarUsuario faz PUT em /api/usuarios/:id', async () => {
  vi.mocked(fetch).mockResolvedValue({ ok: true, status: 200, json: async () => ({ dados: {} }) } as Response)
  await atualizarUsuario('abc', { nomeCompleto: 'Novo', nomeEstabelecimento: 'Loja2' })
  expect(vi.mocked(fetch)).toHaveBeenCalledWith('/api/usuarios/abc', expect.objectContaining({ method: 'PUT' }))
})

test('desativarUsuario faz DELETE em /api/usuarios/:id', async () => {
  vi.mocked(fetch).mockResolvedValue({ ok: true, status: 200, json: async () => ({ dados: null }) } as Response)
  await desativarUsuario('abc')
  expect(vi.mocked(fetch)).toHaveBeenCalledWith('/api/usuarios/abc', expect.objectContaining({ method: 'DELETE' }))
})
