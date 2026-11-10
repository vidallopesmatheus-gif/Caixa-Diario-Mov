import { salvarRascunhos } from '../utils/rascunhos'
import type { LoginResponse } from '../types'

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

// ── Sessão (Money Vision: "desloga toda hora") ──────────────────────────────────────────────
// O access token (JWT curto) vive SÓ em memória — nunca no localStorage, onde qualquer XSS lê.
// Quem mantém a sessão é o refresh token, num cookie HttpOnly que o JS nem enxerga: ao recarregar
// a página ou quando o access token vence, /api/auth/refresh devolve um novo.
let tokenAcesso: string | null = null
let renovacaoEmAndamento: Promise<LoginResponse | null> | null = null
let redirecionandoParaLogin = false
const ouvintesRenovacao = new Set<(sessao: LoginResponse) => void>()

export function definirTokenAcesso(token: string | null) { tokenAcesso = token }

/** AuthContext usa pra manter o usuário (nome, perfil) em dia a cada renovação. */
export function aoRenovarSessao(ouvinte: (sessao: LoginResponse) => void): () => void {
  ouvintesRenovacao.add(ouvinte)
  return () => { ouvintesRenovacao.delete(ouvinte) }
}

// Só tenta renovar quem já entrou alguma vez neste navegador (sem 'user', não há cookie de sessão).
const temSessaoSalva = () => {
  try { return localStorage.getItem('user') !== null } catch { return false }
}

/**
 * Troca o cookie de refresh por um access token novo. Uma única chamada por vez: dez requisições
 * que recebem 401 juntas esperam a mesma renovação, em vez de dispararem dez (e a rotação do
 * refresh token invalidaria as outras nove).
 */
export function renovarSessao(): Promise<LoginResponse | null> {
  renovacaoEmAndamento ??= (async () => {
    try {
      const res = await fetch(`${BASE}/api/auth/refresh`, { method: 'POST', credentials: 'same-origin' })
      if (!res.ok) return null
      const sessao = ((await res.json()) as { dados: LoginResponse }).dados
      tokenAcesso = sessao.token
      ouvintesRenovacao.forEach(o => o(sessao))
      return sessao
    } catch {
      return null
    } finally {
      renovacaoEmAndamento = null
    }
  })()
  return renovacaoEmAndamento
}

/** Refresh também venceu: guarda o que estava digitado e manda pro login com a rota de volta. */
function encerrarSessaoExpirada(): never {
  tokenAcesso = null
  if (!redirecionandoParaLogin && !window.location.pathname.startsWith('/login')) {
    redirecionandoParaLogin = true
    salvarRascunhos()
    localStorage.removeItem('user')
    const voltar = window.location.pathname + window.location.search
    window.location.assign(`/login?expirou=1&voltar=${encodeURIComponent(voltar)}`)
  }
  throw new ApiError('SESSAO_EXPIRADA', 'Sua sessão expirou. Entre de novo para continuar.')
}

/**
 * Faz a requisição autenticada e devolve a Response crua (downloads de arquivo usam isso).
 * 401 → renova a sessão UMA vez e repete a chamada original; só se a renovação falhar vai pro login.
 */
export async function apiFetchResposta(path: string, options: RequestInit = {}): Promise<Response> {
  // Não forçar Content-Type quando o body for FormData (o browser define com boundary)
  const isFormData = options.body instanceof FormData
  // Item 2.10: /api/auth/* (login) é anônimo — nunca deve levar um token antigo/de outro usuário
  // no header, e um 401 aqui é SEMPRE "usuário ou senha incorretos" (erro de negócio vindo do
  // próprio corpo da resposta), nunca "sessão expirada".
  const isAuthRoute = path.startsWith('/api/auth/')
  const metodo = (options.method ?? 'GET').toUpperCase()
  // Money Vision: "500 intermitente ao abrir o Dashboard" — uma leitura (GET, sempre segura de
  // repetir) que falha com 500/503 tenta de novo 1x depois de uma pausa curta.
  const podeTentarDeNovo = metodo === 'GET'

  // Página recém-carregada: o token não sobrevive ao reload (só memória) — renova antes de pedir.
  if (!isAuthRoute && !tokenAcesso && temSessaoSalva()) await renovarSessao()

  const executar = async (): Promise<Response> => fetch(`${BASE}${path}`, {
    ...options,
    credentials: 'same-origin',
    headers: {
      ...(isFormData ? {} : { 'Content-Type': 'application/json' }),
      ...options.headers,
      ...(tokenAcesso && !isAuthRoute ? { Authorization: `Bearer ${tokenAcesso}` } : {}),
    },
  })

  let res = await executar()
  if (podeTentarDeNovo && (res.status === 500 || res.status === 503)) {
    await new Promise(resolve => setTimeout(resolve, 400))
    res = await executar()
  }

  if (res.status === 401 && !isAuthRoute) {
    if (!(await renovarSessao())) encerrarSessaoExpirada()
    res = await executar()
    if (res.status === 401) encerrarSessaoExpirada()
  }
  return res
}

export async function apiFetch<T>(
  path: string,
  options: RequestInit = {}
): Promise<T> {
  const res = await apiFetchResposta(path, options)
  if (!res.ok) {
    const err = await res.json().catch(() => ({}))
    throw new ApiError(err?.codigo ?? 'ERRO_DESCONHECIDO', err?.mensagem ?? `Erro ${res.status}`, err?.campo)
  }
  return res.json()
}
