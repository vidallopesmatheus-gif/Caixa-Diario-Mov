import { agruparPorDescricaoSimilar, removerPrefixoRecorrente, mascararDocumentos } from './descricaoSimilar'

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

// Item 1.9: identificador numérico (CPF/CNPJ parcial, número de conta/cliente etc.) às vezes
// aparece em posições diferentes entre lançamentos do MESMO favorecido — ex.: "53.430.745 POLLY
// SILVA" num lançamento e "POLLY SILVA (53.430.745)" noutro. agruparPorDescricaoSimilar já cobre
// documento completo (CPF/CNPJ) e prefixo comum do lote, mas não essa troca de posição, porque
// nesse caso não há prefixo comum nenhum entre as duas descrições. Remove todo ruído numérico do
// rótulo pra enxergar o nome puro por trás da variação.
const REGEX_RUIDO_NUMERICO = /\(?\s*\d[\d.\-/]{4,}\s*\)?/g
const TAMANHO_MINIMO_NOME = 3

function chaveNomeNormalizada(rotulo: string): string {
  return rotulo.replace(REGEX_RUIDO_NUMERICO, ' ').replace(/\s{2,}/g, ' ').trim().toUpperCase()
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

  const brutos = grupos.map(g => {
    // Rótulo legível: mesma remoção de prefixo, agora contra o lote inteiro — inclusive quando
    // o agrupamento usou CNPJ/CPF como chave (agruparPorDescricaoSimilar não calcula rótulo
    // nesse caso, só a chave de agrupamento).
    const idxRepresentante = comId.findIndex(i => i.id === g.itens[0].id)
    const rotuloBruto = removerPrefixoRecorrente(normalizadas[idxRepresentante], normalizadas) || g.itens[0].descricao
    // Mascara CPF/CNPJ mesmo aqui: a remoção de prefixo às vezes não consegue cortar o documento
    // (ex.: ele aparece no meio ou no fim da descrição, não no prefixo comum ao lote), e o
    // fallback pra descrição bruta nunca deve expor o documento completo no rótulo visível.
    const rotulo = mascararDocumentos(rotuloBruto)

    return {
      rotulo,
      total: g.itens.reduce((s, i) => s + i.valor, 0),
      ocorrencias: g.itens.length,
      lancamentos: g.itens.map(i => ({ data: i.data, descricao: i.descricao, valor: i.valor })),
    }
  })

  // Segunda passada: funde grupos cujo rótulo, depois de limpo o ruído numérico, é o mesmo nome.
  const fundidos = new Map<string, RankingFavorecido>()
  let proximoUnico = 0
  for (const g of brutos) {
    const nomeChave = chaveNomeNormalizada(g.rotulo)
    const chave = nomeChave.length >= TAMANHO_MINIMO_NOME ? nomeChave : `__UNICO__${proximoUnico++}`
    const existente = fundidos.get(chave)
    if (existente) {
      existente.total += g.total
      existente.ocorrencias += g.ocorrencias
      existente.lancamentos.push(...g.lancamentos)
      existente.rotulo = nomeChave // fundiu variações do mesmo nome: usa a versão sem ruído numérico
    } else {
      fundidos.set(chave, { ...g, lancamentos: [...g.lancamentos] })
    }
  }

  return Array.from(fundidos.values())
    .sort((a, b) => b.total - a.total)
    .slice(0, limite)
}
