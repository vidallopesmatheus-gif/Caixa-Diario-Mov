const BASE = import.meta.env.VITE_API_URL ?? ''

// Cliente HTTP dedicado ao portal público (sem login) — nunca anexa o Bearer do consultor
// nem redireciona pra /login em erro, diferente de apiFetch. O código de erro vem intacto
// pra quem chama decidir a UI (ex.: link expirado tem uma tela própria, não um erro genérico).
export class PortalApiError extends Error {
  codigo: string
  constructor(codigo: string, mensagem: string) {
    super(mensagem)
    this.codigo = codigo
  }
}

export async function portalFetch<T>(path: string, options: RequestInit = {}): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...options.headers,
    },
  })
  const body = await res.json().catch(() => ({}))
  if (!res.ok) {
    throw new PortalApiError(body?.codigo ?? 'ERRO_DESCONHECIDO', body?.mensagem ?? `Erro ${res.status}`)
  }
  return body as T
}
