import { ehOperacional } from './lancamentos'

const NOMES_DIA_SEMANA = ['Domingo', 'Segunda-feira', 'Terça-feira', 'Quarta-feira', 'Quinta-feira', 'Sexta-feira', 'Sábado']
const NOMES_MES = ['Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun', 'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez']

interface RegistroParaConcentracao {
  data: string // "yyyy-MM-dd"
  entradas: { valor: number; tipoCusto?: string }[]
  saidas: { valor: number; tipoCusto?: string }[]
}

export interface Ponto { rotulo: string; total: number }

export interface ConcentracaoPorDia {
  granularidade: 'dia' | 'mes'
  entradaDiaSemana: Ponto | null
  entradaDiaMes: Ponto | null
  saidaDiaSemana: Ponto | null
  saidaDiaMes: Ponto | null
  entradaMes: Ponto | null
  saidaMes: Ponto | null
}

function maiorPonto(mapa: Map<string, number>): Ponto | null {
  let melhor: Ponto | null = null
  for (const [rotulo, total] of mapa) {
    if (!melhor || total > melhor.total) melhor = { rotulo, total }
  }
  return melhor
}

function somarPorChave(
  registros: RegistroParaConcentracao[],
  campo: 'entradas' | 'saidas',
  chaveDe: (data: string) => string,
): Map<string, number> {
  const mapa = new Map<string, number>()
  for (const r of registros) {
    // Mesma regra de exclusão do DRE: transferência/rendimento/investimento/financiamento não
    // são volume de entrada/saída "de negócio" — não entram na concentração.
    const soma = r[campo].filter(ehOperacional).reduce((s, i) => s + i.valor, 0)
    if (soma <= 0) continue
    const chave = chaveDe(r.data)
    mapa.set(chave, (mapa.get(chave) ?? 0) + soma)
  }
  return mapa
}

/**
 * `granularidade` é decidida pelo chamador a partir do tipo de período selecionado no DRE — mês
 * vira 'dia' (dia da semana + dia do mês), trimestre/ano viram 'mes'. O corte muda sozinho com o
 * filtro de período da tela; esta função não decide isso, só recebe a decisão já tomada.
 */
export function calcularConcentracaoPorDia(
  registros: RegistroParaConcentracao[],
  granularidade: 'dia' | 'mes',
): ConcentracaoPorDia {
  if (granularidade === 'mes') {
    const chaveDe = (data: string) => {
      const [ano, mes] = data.split('-')
      return `${NOMES_MES[Number(mes) - 1]}/${ano}`
    }
    return {
      granularidade,
      entradaDiaSemana: null, entradaDiaMes: null, saidaDiaSemana: null, saidaDiaMes: null,
      entradaMes: maiorPonto(somarPorChave(registros, 'entradas', chaveDe)),
      saidaMes: maiorPonto(somarPorChave(registros, 'saidas', chaveDe)),
    }
  }

  const chaveDiaSemana = (data: string) => NOMES_DIA_SEMANA[new Date(`${data}T12:00:00`).getDay()]
  const chaveDiaMes = (data: string) => String(Number(data.slice(-2)))

  return {
    granularidade,
    entradaDiaSemana: maiorPonto(somarPorChave(registros, 'entradas', chaveDiaSemana)),
    entradaDiaMes: maiorPonto(somarPorChave(registros, 'entradas', chaveDiaMes)),
    saidaDiaSemana: maiorPonto(somarPorChave(registros, 'saidas', chaveDiaSemana)),
    saidaDiaMes: maiorPonto(somarPorChave(registros, 'saidas', chaveDiaMes)),
    entradaMes: null, saidaMes: null,
  }
}
