import { login } from './auth'

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
