import { agruparPorDescricaoSimilar, removerPrefixoRecorrente } from './descricaoSimilar'

/**
 * Ranking de favorecidos (pra quem mais se pagou) ou pagadores (de quem mais se recebeu) num
 * período — reaproveita a mesma heurística de agrupamento por descrição semelhante usada na
 * fila de categorização (agruparPorDescricaoSimilar): CNPJ/CPF como chave quando presente,
 * senão o prefixo recorrente do lote é removido e o que sobra vira a chave do favorecido.
 */
export interface RankingFavorecido {
  rotulo: string
  total: number
  ocorrencias: number
  lancamentos: { data: string; descricao: string; valor: number }[]
}

interface LancamentoParaRanking {
  data: string
  descricao: string
  valor: number
}

export function calcularRankingFavorecidos(
  itens: LancamentoParaRanking[],
  tipo: 'Entrada' | 'Saida',
  limite = 5,
): RankingFavorecido[] {
  if (itens.length === 0) return []

  const comId = itens.map((item, i) => ({ ...item, id: String(i), tipo }))
  const grupos = agruparPorDescricaoSimilar(comId)
  const normalizadas = comId.map(i => i.descricao.toUpperCase().trim())

  return grupos
    .map(g => {
      // Rótulo legível: mesma remoção de prefixo, agora contra o lote inteiro — inclusive quando
      // o agrupamento usou CNPJ/CPF como chave (agruparPorDescricaoSimilar não calcula rótulo
      // nesse caso, só a chave de agrupamento).
      const idxRepresentante = comId.findIndex(i => i.id === g.itens[0].id)
      const rotulo = removerPrefixoRecorrente(normalizadas[idxRepresentante], normalizadas) || g.itens[0].descricao

      return {
        rotulo,
        total: g.itens.reduce((s, i) => s + i.valor, 0),
        ocorrencias: g.itens.length,
        lancamentos: g.itens.map(i => ({ data: i.data, descricao: i.descricao, valor: i.valor })),
      }
    })
    .sort((a, b) => b.total - a.total)
    .slice(0, limite)
}
