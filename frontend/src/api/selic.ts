const CACHE_KEY = 'selic_cache'
const CACHE_TTL_MS = 24 * 60 * 60 * 1000 // 24h
const FALLBACK_SELIC = 10.5 // % a.a. — fallback se API offline

interface SelicCache {
  valor: number
  timestamp: number
}

export async function obterSelicAtual(): Promise<number> {
  try {
    const cached = localStorage.getItem(CACHE_KEY)
    if (cached) {
      const parsed: SelicCache = JSON.parse(cached)
      if (Date.now() - parsed.timestamp < CACHE_TTL_MS) return parsed.valor
    }
  } catch {
    // cache corrompido, ignora
  }

  try {
    const res = await fetch(
      'https://api.bcb.gov.br/dados/serie/bcdata.sgs.11/dados/ultimos/1?formato=json',
      { signal: AbortSignal.timeout(5000) }
    )
    if (!res.ok) throw new Error('BCB offline')
    const data: { valor: string }[] = await res.json()
    const taxa = parseFloat(data[0].valor) // % a.a.
    localStorage.setItem(CACHE_KEY, JSON.stringify({ valor: taxa, timestamp: Date.now() }))
    return taxa
  } catch {
    return FALLBACK_SELIC
  }
}
