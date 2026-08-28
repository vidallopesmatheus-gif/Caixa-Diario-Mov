import type { MetaAnual } from '../types'

export type RitmoStatus = 'sem-dados' | 'atingida' | 'adiantada' | 'no-ritmo' | 'atrasada'

export interface RitmoMeta {
  status: RitmoStatus
  /** Pode passar de 100 (adiantada) — a UI decide se capa para a barra. */
  percentual: number
  dataAlvo: Date | null
  mesesRestantes?: number
  /** Só presente quando status === 'atrasada'. */
  aporteNecessarioAgora?: number
}

function diffMeses(inicio: Date, fim: Date): number {
  return (fim.getFullYear() - inicio.getFullYear()) * 12 + (fim.getMonth() - inicio.getMonth())
}

/**
 * Mesma matemática usada na edição de meta (compara o aporte necessário "agora",
 * projetado sobre o prazo restante, com o aporte originalmente planejado sobre o
 * prazo total) — só generalizada para funcionar com qualquer meta selecionada,
 * não apenas a do ano corrente em edição.
 */
export function calcularRitmoMeta(meta: MetaAnual): RitmoMeta {
  const vf = meta.valorSonho
  const taxa = meta.taxaRetorno
  const prazoTotalMeses = meta.prazoAnos * 12
  const investido = meta.totalInvestido
  const percentual = vf > 0 ? (investido / vf) * 100 : 0

  const dataInicio = meta.salvoEm ? new Date(meta.salvoEm) : null
  const dataAlvo = dataInicio && prazoTotalMeses > 0
    ? new Date(dataInicio.getFullYear(), dataInicio.getMonth() + prazoTotalMeses, dataInicio.getDate())
    : null

  if (!vf || vf <= 0) return { status: 'sem-dados', percentual: 0, dataAlvo: null }
  if (investido >= vf) return { status: 'atingida', percentual, dataAlvo }
  if (!taxa || taxa <= 0 || prazoTotalMeses <= 0 || !dataInicio) return { status: 'sem-dados', percentual, dataAlvo }

  const mesesDecorridos = diffMeses(dataInicio, new Date())
  const mesesRestantes = prazoTotalMeses - mesesDecorridos
  if (mesesDecorridos <= 0) return { status: 'no-ritmo', percentual, dataAlvo, mesesRestantes }
  if (mesesRestantes <= 1) return { status: 'atrasada', percentual, dataAlvo, mesesRestantes: Math.max(0, mesesRestantes) }

  const i = Math.pow(1 + taxa / 100, 1 / 12) - 1

  // Aporte originalmente planejado (referência: prazo total, mesma fórmula da tela de edição).
  const fvAtualPlano = investido * Math.pow(1 + i, prazoTotalMeses)
  const fvNecessarioPlano = vf - fvAtualPlano
  const aporteMensalPlanejado = fvNecessarioPlano > 0
    ? (fvNecessarioPlano * i) / (Math.pow(1 + i, prazoTotalMeses) - 1)
    : 0

  // Aporte necessário agora, projetado só sobre o prazo restante.
  const fvCorrigido = investido * Math.pow(1 + i, mesesRestantes)
  const fvNecessario = vf - fvCorrigido
  if (fvNecessario <= 0) return { status: 'adiantada', percentual, dataAlvo, mesesRestantes }

  const aporteNecessarioAgora = (fvNecessario * i) / (Math.pow(1 + i, mesesRestantes) - 1)

  if (aporteMensalPlanejado <= 0) return { status: 'no-ritmo', percentual, dataAlvo, mesesRestantes }
  if (aporteNecessarioAgora <= aporteMensalPlanejado * 0.9) return { status: 'adiantada', percentual, dataAlvo, mesesRestantes }
  if (aporteNecessarioAgora <= aporteMensalPlanejado * 1.1) return { status: 'no-ritmo', percentual, dataAlvo, mesesRestantes }
  return { status: 'atrasada', percentual, dataAlvo, mesesRestantes, aporteNecessarioAgora }
}
