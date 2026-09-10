const CACHE_KEY = 'selic_cache'
const CACHE_TTL_MS = 24 * 60 * 60 * 1000 // 24h
const FALLBACK_SELIC = 10.5 // % a.a. — usado só se a API do BC estiver indisponível e não houver cache

interface SelicCache {
  valor: number
  timestamp: number
}

export interface SelicResultado {
  valor: number
  /** 'bcb' = veio agora da API do Banco Central. 'cache' = veio do cache local (até 24h). 'indisponivel' = API falhou e não havia cache — valor é um fallback fixo, não a SELIC real. */
  fonte: 'bcb' | 'cache' | 'indisponivel'
}

// Série 432 do SGS/BCB = "Meta Selic definida pelo Copom", já em % a.a. (ex.: "14.00").
// A série 11 ("Taxa Selic") é a taxa DIÁRIA (ex.: "0.051660" = % ao dia) — usá-la como se fosse
// anual é o que produzia "SELIC (0,05% a.a.)" sem correção alguma no composto.
const SERIE_SELIC_META_ANUAL = 432

export async function obterSelicAtual(): Promise<SelicResultado> {
  try {
    const cached = localStorage.getItem(CACHE_KEY)
    if (cached) {
      const parsed: SelicCache = JSON.parse(cached)
      if (Date.now() - parsed.timestamp < CACHE_TTL_MS) return { valor: parsed.valor, fonte: 'cache' }
    }
  } catch {
    // cache corrompido, ignora
  }

  try {
    const res = await fetch(
      `https://api.bcb.gov.br/dados/serie/bcdata.sgs.${SERIE_SELIC_META_ANUAL}/dados/ultimos/1?formato=json`,
      { signal: AbortSignal.timeout(5000) }
    )
    if (!res.ok) throw new Error('BCB offline')
    const data: { valor: string }[] = await res.json()
    const taxa = parseFloat(data[0].valor) // % a.a.
    if (!Number.isFinite(taxa) || taxa <= 0) throw new Error('BCB retornou valor inválido')
    localStorage.setItem(CACHE_KEY, JSON.stringify({ valor: taxa, timestamp: Date.now() }))
    return { valor: taxa, fonte: 'bcb' }
  } catch {
    return { valor: FALLBACK_SELIC, fonte: 'indisponivel' }
  }
}
