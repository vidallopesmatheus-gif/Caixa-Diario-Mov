/**
 * Heurística de agrupamento visual por descrição semelhante — espelha `DescricaoSimilar`
 * do backend (primeiros 20 caracteres em maiúsculas). Só agrupa a exibição na tela de
 * revisão; não recalcula duplicidade (isso continua vindo do backend).
 */
export function chaveSimilaridade(descricao: string): string {
  const upper = descricao.toUpperCase()
  return upper.length >= 20 ? upper.slice(0, 20) : upper
}

export interface GrupoSimilar<T> {
  chave: string
  itens: T[]
}

export function agruparPorDescricaoSimilar<T extends { descricao: string; tipo: 'Entrada' | 'Saida' }>(
  itens: T[],
): GrupoSimilar<T>[] {
  const mapa = new Map<string, T[]>()
  for (const item of itens) {
    const chave = `${item.tipo}::${chaveSimilaridade(item.descricao)}`
    const lista = mapa.get(chave)
    if (lista) lista.push(item)
    else mapa.set(chave, [item])
  }
  return Array.from(mapa.entries()).map(([chave, itens]) => ({ chave, itens }))
}
