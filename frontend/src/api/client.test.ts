import { apiFetch } from './client'

const originalLocation = window.location

beforeEach(() => {
  localStorage.clear()
  vi.stubGlobal('fetch', vi.fn())
  Object.defineProperty(window, 'location', { writable: true, value: { href: '' } })
})

afterEach(() => {
  vi.unstubAllGlobals()
  Object.defineProperty(window, 'location', { writable: true, value: originalLocation })
})

function mockFetch(status: number, body: unknown) {
  vi.mocked(fetch).mockResolvedValue({
    ok: status >= 200 && status < 300,
    status,
    json: async () => body,
  } as Response)
}

test('inclui Content-Type application/json em todas as requisições', async () => {
  mockFetch(200, { dados: null })
  await apiFetch('/api/test')
  expect(vi.mocked(fetch)).toHaveBeenCalledWith(
    '/api/test',
    expect.objectContaining({
      headers: expect.objectContaining({ 'Content-Type': 'application/json' }),
    })
  )
})

test('inclui Authorization Bearer quando token existe no localStorage', async () => {
  localStorage.setItem('token', 'meu-token')
  mockFetch(200, { dados: null })
  await apiFetch('/api/test')
  expect(vi.mocked(fetch)).toHaveBeenCalledWith(
    '/api/test',
    expect.objectContaining({
      headers: expect.objectContaining({ Authorization: 'Bearer meu-token' }),
    })
  )
})

test('não inclui Authorization quando localStorage não tem token', async () => {
  mockFetch(200, { dados: null })
  await apiFetch('/api/test')
  const call = vi.mocked(fetch).mock.calls[0][1] as RequestInit
  expect((call.headers as Record<string, string>).Authorization).toBeUndefined()
})

test('lança Error com mensagem da API quando !res.ok', async () => {
  mockFetch(400, { mensagem: 'Dados inválidos' })
  await expect(apiFetch('/api/test')).rejects.toThrow('Dados inválidos')
})

test('lança Error com "Erro 500" quando body não tem mensagem', async () => {
  mockFetch(500, {})
  await expect(apiFetch('/api/test')).rejects.toThrow('Erro 500')
})

test('limpa localStorage e redireciona para /login quando status 401', async () => {
  localStorage.setItem('token', 'tok')
  localStorage.setItem('user', '{}')
  mockFetch(401, { mensagem: 'Não autorizado' })
  await expect(apiFetch('/api/test')).rejects.toThrow('Sessão expirada')
  expect(localStorage.getItem('token')).toBeNull()
  expect(localStorage.getItem('user')).toBeNull()
  expect(window.location.href).toBe('/login')
})

test('retorna JSON parseado quando resposta ok', async () => {
  mockFetch(200, { dados: [1, 2, 3] })
  const result = await apiFetch<{ dados: number[] }>('/api/test')
  expect(result.dados).toEqual([1, 2, 3])
})

// Item 2.10: /api/auth/* (login) é anônimo — nunca leva um token de outra sessão, e um 401 aqui
// é erro de credenciais (vem com mensagem própria no corpo), nunca "sessão expirada".
test('não inclui Authorization em /api/auth/* mesmo com token no localStorage', async () => {
  localStorage.setItem('token', 'token-de-outra-sessao')
  mockFetch(200, { dados: null })
  await apiFetch('/api/auth/login')
  const call = vi.mocked(fetch).mock.calls[0][1] as RequestInit
  expect((call.headers as Record<string, string>).Authorization).toBeUndefined()
})

test('401 em /api/auth/* não apaga o localStorage nem redireciona — lança o erro da API', async () => {
  localStorage.setItem('token', 'token-de-outra-sessao')
  localStorage.setItem('user', '{}')
  mockFetch(401, { mensagem: 'Usuário ou senha incorretos.' })
  await expect(apiFetch('/api/auth/login')).rejects.toThrow('Usuário ou senha incorretos.')
  expect(localStorage.getItem('token')).toBe('token-de-outra-sessao')
  expect(localStorage.getItem('user')).toBe('{}')
  expect(window.location.href).toBe('')
})
