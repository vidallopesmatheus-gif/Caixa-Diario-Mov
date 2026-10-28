const BASE = import.meta.env.VITE_API_URL ?? ''

// Carrega o código de erro intacto (ex.: "REGRA_CONFLITANTE") pra quem chama decidir a UI,
// em vez de só ter a mensagem — mesma ideia do PortalApiError, mas pro cliente autenticado.
export class ApiError extends Error {
  codigo: string
  campo?: string
  constructor(codigo: string, mensagem: string, campo?: string) {
    super(mensagem)
    this.codigo = codigo
    this.campo = campo
  }
}

export async function apiFetch<T>(
  path: string,
  options: RequestInit = {}
): Promise<T> {
  const token = localStorage.getItem('token')
  // Não forçar Content-Type quando o body for FormData (o browser define com boundary)
  const isFormData = options.body instanceof FormData
  // Item 2.10: /api/auth/* (login) é anônimo — nunca deve levar um token antigo/de outro usuário
  // no header, e um 401 aqui é SEMPRE "usuário ou senha incorretos" (erro de negócio vindo do
  // próprio corpo da resposta), nunca "sessão expirada". Tratar os dois igual apagava o token
  // certo e mostrava "Sessão expirada" bem na tela de login, escondendo a mensagem real da API.
  const isAuthRoute = path.startsWith('/api/auth/')
  const metodo = (options.method ?? 'GET').toUpperCase()
  // Money Vision: "500 intermitente ao abrir o Dashboard" — rajadas de GET em paralelo podiam
  // esgotar o pool de conexões por um instante; uma leitura (GET, sempre segura de repetir) que
  // falha com 500/503 tenta de novo 1x depois de uma pausa curta antes de mostrar erro, em vez de
  // derrubar a tela na primeira falha transitória.
  const podeTentarDeNovo = metodo === 'GET'

  const executar = async (): Promise<Response> => fetch(`${BASE}${path}`, {
    ...options,
    headers: {
      ...(isFormData ? {} : { 'Content-Type': 'application/json' }),
      ...options.headers,
      ...(token && !isAuthRoute ? { Authorization: `Bearer ${token}` } : {}),
    },
  })

  let res = await executar()
  if (podeTentarDeNovo && (res.status === 500 || res.status === 503)) {
    await new Promise(resolve => setTimeout(resolve, 400))
    res = await executar()
  }

  if (res.status === 401 && !isAuthRoute) {
    localStorage.removeItem('token')
    localStorage.removeItem('user')
    window.location.href = '/login'
    throw new Error('Sessão expirada')
  }
  if (!res.ok) {
    const err = await res.json().catch(() => ({}))
    throw new ApiError(err?.codigo ?? 'ERRO_DESCONHECIDO', err?.mensagem ?? `Erro ${res.status}`, err?.campo)
  }
  return res.json()
}
