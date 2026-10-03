const BASE = import.meta.env.VITE_API_URL ?? ''

// Preserva o código de erro do backend (ex.: "REGRA_CONFLITANTE") pra quem chama decidir o que
// fazer, além da mensagem genérica — mesmo padrão de PortalApiError (api/portalClient.ts), só que
// pro cliente autenticado em vez do portal público.
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
  const res = await fetch(`${BASE}${path}`, {
    ...options,
    headers: {
      ...(isFormData ? {} : { 'Content-Type': 'application/json' }),
      ...options.headers,
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
  })
  if (res.status === 401) {
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
