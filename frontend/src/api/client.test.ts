// O token de acesso vive em memória dentro do módulo — cada teste importa o client "do zero"
// (vi.resetModules) pra nunca herdar sessão/redirect de um teste anterior.
type Client = typeof import('./client')
type Rascunhos = typeof import('../utils/rascunhos')

const originalLocation = window.location
let client: Client
let rascunhos: Rascunhos

// bare localStorage/sessionStorage não existem neste ambiente de teste (jsdom/vitest).
function criarStorageFake(): Storage {
  const dados = new Map<string, string>()
  return {
    get length() { return dados.size },
    key: (i: number) => [...dados.keys()][i] ?? null,
    getItem: (k: string) => dados.get(k) ?? null,
    setItem: (k: string, v: string) => { dados.set(k, v) },
    removeItem: (k: string) => { dados.delete(k) },
    clear: () => { dados.clear() },
  }
}

function resposta(status: number, body: unknown): Response {
  return { ok: status >= 200 && status < 300, status, json: async () => body, blob: async () => new Blob() } as Response
}

// Fila de respostas por URL: o 1º fetch de /api/x recebe a 1ª, o 2º a 2ª, e assim por diante.
function servidor(rotas: Record<string, Response[]>) {
  vi.mocked(fetch).mockImplementation(async input => {
    const fila = rotas[String(input)]
    if (!fila || fila.length === 0) throw new Error(`fetch inesperado: ${String(input)}`)
    return fila.length > 1 ? fila.shift()! : fila[0]
  })
}

const sessaoRenovada = { dados: { token: 'token-novo', usuarioId: '1', perfil: 'cliente', nomeUsuario: 'joao', nomeCompleto: 'João', nomeEstabelecimento: '' } }
const chamadasA = (url: string) => vi.mocked(fetch).mock.calls.filter(c => c[0] === url)
const authorizationDa = (chamada: unknown[]) => ((chamada[1] as RequestInit).headers as Record<string, string>).Authorization

beforeEach(async () => {
  vi.resetModules()
  vi.stubGlobal('localStorage', criarStorageFake())
  vi.stubGlobal('sessionStorage', criarStorageFake())
  vi.stubGlobal('fetch', vi.fn())
  Object.defineProperty(window, 'location', {
    writable: true, value: { pathname: '/banco/conta-1', search: '?aba=extrato', assign: vi.fn() },
  })
  client = await import('./client')
  rascunhos = await import('../utils/rascunhos')
})

afterEach(() => {
  vi.unstubAllGlobals()
  Object.defineProperty(window, 'location', { writable: true, value: originalLocation })
})

describe('apiFetch', () => {
  it('should send JSON content type and the in-memory access token', async () => {
    client.definirTokenAcesso('meu-token')
    servidor({ '/api/test': [resposta(200, { dados: null })] })

    await client.apiFetch('/api/test')

    const [, init] = vi.mocked(fetch).mock.calls[0]
    expect(init?.headers).toEqual(expect.objectContaining({ 'Content-Type': 'application/json', Authorization: 'Bearer meu-token' }))
  })

  it('should never read the token from localStorage', async () => {
    localStorage.setItem('token', 'token-antigo-no-localstorage')
    servidor({ '/api/test': [resposta(200, { dados: null })] })

    await client.apiFetch('/api/test')

    expect(authorizationDa(vi.mocked(fetch).mock.calls[0])).toBeUndefined()
  })

  it('should renew the session and retry the original call when the access token expired', async () => {
    client.definirTokenAcesso('token-vencido')
    localStorage.setItem('user', '{}')
    servidor({
      '/api/test': [resposta(401, {}), resposta(200, { dados: 'ok' })],
      '/api/auth/refresh': [resposta(200, sessaoRenovada)],
    })

    const resultado = await client.apiFetch<{ dados: string }>('/api/test')

    expect(resultado.dados).toBe('ok')
    expect(authorizationDa(chamadasA('/api/test')[1])).toBe('Bearer token-novo')
    expect(window.location.assign).not.toHaveBeenCalled()
  })

  it('should renew once on page load when there is a saved user but no token in memory', async () => {
    localStorage.setItem('user', '{}')
    servidor({ '/api/auth/refresh': [resposta(200, sessaoRenovada)], '/api/test': [resposta(200, { dados: null })] })

    await client.apiFetch('/api/test')

    expect(chamadasA('/api/auth/refresh')).toHaveLength(1)
    expect(authorizationDa(chamadasA('/api/test')[0])).toBe('Bearer token-novo')
  })

  it('should share a single refresh between parallel calls that got 401', async () => {
    client.definirTokenAcesso('token-vencido')
    localStorage.setItem('user', '{}')
    let liberarRefresh!: () => void
    const refreshPendente = new Promise<Response>(resolve => { liberarRefresh = () => resolve(resposta(200, sessaoRenovada)) })
    vi.mocked(fetch).mockImplementation(async (input, init) => {
      if (input === '/api/auth/refresh') return refreshPendente
      const auth = ((init as RequestInit).headers as Record<string, string>).Authorization
      return auth === 'Bearer token-novo' ? resposta(200, { dados: input }) : resposta(401, {})
    })

    const chamadas = Promise.all([client.apiFetch('/api/a'), client.apiFetch('/api/b'), client.apiFetch('/api/c')])
    await vi.waitFor(() => expect(chamadasA('/api/auth/refresh')).toHaveLength(1))
    liberarRefresh()
    await chamadas

    expect(chamadasA('/api/auth/refresh')).toHaveLength(1)
  })

  it('should save open drafts and go to login with the return route when the refresh also expired', async () => {
    client.definirTokenAcesso('token-vencido')
    localStorage.setItem('user', '{}')
    rascunhos.registrarRascunho('caixa-dia', () => ({ descricao: 'Venda do dia' }))
    servidor({ '/api/test': [resposta(401, {})], '/api/auth/refresh': [resposta(401, {})] })

    await expect(client.apiFetch('/api/test')).rejects.toThrow('Sua sessão expirou')

    expect(window.location.assign).toHaveBeenCalledWith('/login?expirou=1&voltar=%2Fbanco%2Fconta-1%3Faba%3Dextrato')
    expect(rascunhos.lerRascunho('caixa-dia')).toEqual({ descricao: 'Venda do dia' })
    expect(localStorage.getItem('user')).toBeNull()
  })

  it('should not send a token nor try to renew on /api/auth routes when login fails', async () => {
    client.definirTokenAcesso('token-de-outra-sessao')
    localStorage.setItem('user', '{}')
    servidor({ '/api/auth/login': [resposta(401, { mensagem: 'Usuário ou senha incorretos.' })] })

    await expect(client.apiFetch('/api/auth/login')).rejects.toThrow('Usuário ou senha incorretos.')

    expect(authorizationDa(vi.mocked(fetch).mock.calls[0])).toBeUndefined()
    expect(vi.mocked(fetch)).toHaveBeenCalledTimes(1)
    expect(window.location.assign).not.toHaveBeenCalled()
  })

  it('should throw the API message when the response is not ok', async () => {
    servidor({ '/api/test': [resposta(400, { mensagem: 'Dados inválidos', codigo: 'DADOS_INVALIDOS' })] })

    await expect(client.apiFetch('/api/test')).rejects.toMatchObject({ message: 'Dados inválidos', codigo: 'DADOS_INVALIDOS' })
  })

  it('should retry a GET once when the server answers 500', async () => {
    servidor({ '/api/test': [resposta(500, {}), resposta(200, { dados: [1, 2, 3] })] })

    const resultado = await client.apiFetch<{ dados: number[] }>('/api/test')

    expect(resultado.dados).toEqual([1, 2, 3])
  })
})
