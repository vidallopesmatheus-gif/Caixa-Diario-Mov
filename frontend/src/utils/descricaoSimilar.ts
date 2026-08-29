/**
 * Heurística de agrupamento visual por descrição semelhante, usada na tela de revisão de
 * extrato para juntar lançamentos "parecidos" e permitir categorizar todos de uma vez.
 *
 * Extratos bancários costumam usar um prefixo longo e repetitivo ("Transferência enviada
 * pelo Pix - ", "Compra no débito - ") e só o final da descrição (favorecido, CNPJ/CPF)
 * distingue uma transação da outra. Comparar a descrição inteira (ou só o começo dela)
 * agrupa transações completamente diferentes só porque compartilham o prefixo — por isso
 * o critério aqui descarta o prefixo recorrente do LOTE (não uma lista fixa de prefixos
 * conhecidos, que não cobriria todo banco) e compara o que sobra. Quando o prefixo consome
 * quase a descrição inteira (nada sobra pra comparar), a heurística prefere NÃO agrupar —
 * agrupar errado faz o usuário categorizar lançamentos diferentes com a mesma categoria,
 * corrompendo o DRE.
 */

const REGEX_CNPJ = /\d{2}\.?\d{3}\.?\d{3}\/?\d{4}-?\d{2}/
const REGEX_CPF = /\d{3}\.?\d{3}\.?\d{3}-?\d{2}/
const TAMANHO_MINIMO_PREFIXO = 12
const TAMANHO_MINIMO_CAUDA = 3

/** CNPJ/CPF na descrição é o identificador mais confiável do favorecido, quando presente. */
function extrairDocumento(descricao: string): string | null {
  const cnpj = descricao.match(REGEX_CNPJ)
  if (cnpj) return `DOC:${cnpj[0].replace(/\D/g, '')}`
  const cpf = descricao.match(REGEX_CPF)
  if (cpf) return `DOC:${cpf[0].replace(/\D/g, '')}`
  return null
}

function tamanhoPrefixoComum(a: string, b: string): number {
  const max = Math.min(a.length, b.length)
  let i = 0
  while (i < max && a[i] === b[i]) i++
  return i
}

/** Recua até o limite de palavra/pontuação mais próximo, pra não cortar no meio de um nome. */
function ajustarParaLimiteDePalavra(texto: string, tamanho: number): number {
  let i = Math.min(tamanho, texto.length)
  while (i > 0 && texto[i - 1] !== ' ' && !'-–:/'.includes(texto[i - 1])) i--
  return i > 0 ? i : tamanho
}

/** Remove, de uma descrição, o maior prefixo que ela compartilha com outra do mesmo lote. */
function removerPrefixoRecorrente(descricaoNormalizada: string, outrasNormalizadas: string[]): string {
  let maiorPrefixo = 0
  for (const outra of outrasNormalizadas) {
    if (outra === descricaoNormalizada) continue
    const tamanho = tamanhoPrefixoComum(descricaoNormalizada, outra)
    if (tamanho > maiorPrefixo) maiorPrefixo = tamanho
  }
  if (maiorPrefixo < TAMANHO_MINIMO_PREFIXO) return descricaoNormalizada

  const corte = ajustarParaLimiteDePalavra(descricaoNormalizada, maiorPrefixo)
  return descricaoNormalizada.slice(corte).replace(/^[\s\-–:/]+/, '').trim()
}

export interface GrupoSimilar<T> {
  chave: string
  itens: T[]
}

export function agruparPorDescricaoSimilar<T extends { id: string; descricao: string; tipo: 'Entrada' | 'Saida' }>(
  itens: T[],
): GrupoSimilar<T>[] {
  const porTipo = new Map<'Entrada' | 'Saida', T[]>()
  for (const item of itens) {
    const lista = porTipo.get(item.tipo)
    if (lista) lista.push(item)
    else porTipo.set(item.tipo, [item])
  }

  const mapa = new Map<string, T[]>()
  for (const [tipo, lista] of porTipo) {
    const normalizadas = lista.map(i => i.descricao.toUpperCase().trim())
    lista.forEach((item, idx) => {
      const doc = extrairDocumento(item.descricao)
      const cauda = doc ?? removerPrefixoRecorrente(normalizadas[idx], normalizadas)
      // Sobrou pouco ou nada além do prefixo comum: melhor não agrupar do que agrupar errado.
      const chave = cauda.length >= TAMANHO_MINIMO_CAUDA
        ? `${tipo}::${cauda}`
        : `${tipo}::__UNICO__${item.id}`
      const grupo = mapa.get(chave)
      if (grupo) grupo.push(item)
      else mapa.set(chave, [item])
    })
  }

  return Array.from(mapa.entries()).map(([chave, itens]) => ({ chave, itens }))
}
