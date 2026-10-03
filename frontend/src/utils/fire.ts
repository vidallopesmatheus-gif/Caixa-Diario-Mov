import type { Registro, CategoriaAdmin } from '../types'
import { ehOperacional } from './lancamentos'

export type OrigemCustoVida = 'manual' | 'automatico' | 'vazio'

export interface CustoDeVidaResultado {
  valor: number | null
  origem: OrigemCustoVida
  /** Quantos dos últimos 12 meses fechados tinham alguma saída de categoria pessoal — só relevante quando origem === 'automatico' ou 'vazio'. */
  mesesConsiderados: number
}

const MESES_JANELA = 12
const MESES_MINIMO = 3

function chaveMes(ano: number, mes1a12: number): string {
  return `${ano}-${String(mes1a12).padStart(2, '0')}`
}

/**
 * Custo de vida pessoal mensal — base do Indicador FIRE (nunca a despesa total do negócio):
 *  1) valor manual configurado pelo usuário, quando presente;
 *  2) senão, média das saídas de categorias marcadas "conta pro custo de vida" (Pró-labore,
 *     Retirada de Sócio...) nos últimos 12 meses FECHADOS (o mês corrente nunca entra — está
 *     incompleto), exigindo pelo menos 3 meses com algum dado pra confiar na média;
 *  3) senão, estado vazio — quem chama decide o que mostrar (nunca cai pra despesa total).
 */
export function calcularCustoDeVida(
  registros: Registro[],
  categorias: CategoriaAdmin[],
  custoVidaManual: number | null | undefined,
  hoje: Date = new Date(),
): CustoDeVidaResultado {
  if (custoVidaManual != null && custoVidaManual > 0)
    return { valor: custoVidaManual, origem: 'manual', mesesConsiderados: 0 }

  const categoriasPessoais = new Set(categorias.filter(c => c.ehPessoal).map(c => c.nome))
  if (categoriasPessoais.size === 0) return { valor: null, origem: 'vazio', mesesConsiderados: 0 }

  const mesesFechados = new Set<string>()
  for (let i = 1; i <= MESES_JANELA; i++) {
    const d = new Date(hoje.getFullYear(), hoje.getMonth() - i, 1)
    mesesFechados.add(chaveMes(d.getFullYear(), d.getMonth() + 1))
  }

  const totalPorMes = new Map<string, number>()
  for (const r of registros) {
    const chave = r.data.slice(0, 7)
    if (!mesesFechados.has(chave)) continue
    for (const s of r.saidas) {
      if (!ehOperacional(s) || !s.categoria || !categoriasPessoais.has(s.categoria)) continue
      totalPorMes.set(chave, (totalPorMes.get(chave) ?? 0) + s.valor)
    }
  }

  if (totalPorMes.size < MESES_MINIMO) return { valor: null, origem: 'vazio', mesesConsiderados: totalPorMes.size }

  const total = Array.from(totalPorMes.values()).reduce((a, b) => a + b, 0)
  return { valor: total / totalPorMes.size, origem: 'automatico', mesesConsiderados: totalPorMes.size }
}

export interface FireResultado {
  /** Patrimônio necessário pra viver de renda (custo de vida anual ÷ taxa de retirada). */
  valorAlvo: number
  /** 0–100. */
  percentualAtingido: number
  faltam: number
  atingido: boolean
}

/** ValorAlvo = CustoVidaMensal × 12 ÷ TaxaRetirada — nunca usa a despesa total do negócio. */
export function calcularFire(
  custoVidaMensal: number | null | undefined,
  taxaRetiradaPercentual: number | null | undefined,
  capitalInvestido: number | null | undefined,
): FireResultado | null {
  if (!custoVidaMensal || custoVidaMensal <= 0) return null
  if (!taxaRetiradaPercentual || taxaRetiradaPercentual <= 0) return null
  const capital = capitalInvestido ?? 0

  const valorAlvo = (custoVidaMensal * 12) / (taxaRetiradaPercentual / 100)
  if (!Number.isFinite(valorAlvo) || valorAlvo <= 0) return null

  const percentualAtingido = Math.min(100, Math.max(0, (capital / valorAlvo) * 100))
  const faltam = Math.max(0, valorAlvo - capital)

  return { valorAlvo, percentualAtingido, faltam, atingido: capital >= valorAlvo }
}
