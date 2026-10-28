// Money Vision: "Dashboard dispara a mesma chamada 2x (ou mais) na mesma abertura" — várias seções
// (resumo, DRE do planejamento mês a mês, cards de dívidas/evolução) pedem a MESMA métrica
// (cliente + período) quase ao mesmo tempo, sem saber umas das outras. Isso não só desperdiça
// banda — era um dos fatores que esgotava o pool de conexões do Supabase e causava os 500
// intermitentes ao abrir o Dashboard. cachedFetch funde chamadas concorrentes com a MESMA chave
// numa promise só, e guarda o resultado por um tempo curto (padrão 30s) — curto o bastante pra
// nunca mostrar dado visivelmente desatualizado numa sessão normal, mas o bastante pra absorver a
// rajada de chamadas que uma única abertura de tela dispara.
const emAndamento = new Map<string, Promise<unknown>>()
const cache = new Map<string, { valor: unknown; expiraEm: number }>()

export function cachedFetch<T>(chave: string, buscar: () => Promise<T>, ttlMs = 30_000): Promise<T> {
  const agora = Date.now()
  const cacheado = cache.get(chave)
  if (cacheado && cacheado.expiraEm > agora) return Promise.resolve(cacheado.valor as T)

  const pendente = emAndamento.get(chave)
  if (pendente) return pendente as Promise<T>

  const promise = buscar()
    .then(valor => {
      cache.set(chave, { valor, expiraEm: Date.now() + ttlMs })
      return valor
    })
    .finally(() => emAndamento.delete(chave))
  emAndamento.set(chave, promise)
  return promise
}

// Chamado depois de qualquer ação que muda lançamentos/contas/metas — descarta tudo em cache em
// vez de tentar rastrear qual chave específica ficou obsoleta (o TTL curto já limita o estrago de
// simplesmente não invalidar nada; isto é só pra quando se sabe que mudou algo agora mesmo).
export function invalidarCache(prefixo?: string): void {
  if (!prefixo) { cache.clear(); return }
  for (const chave of cache.keys()) if (chave.startsWith(prefixo)) cache.delete(chave)
}
