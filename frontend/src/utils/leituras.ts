import { fmtBRL, fmtPct } from './format'
import type { PontoEquilibrio, FolegoCaixa, PrazoRecebimento } from '../api/metricas'

/** Leitura textual simples da margem do DRE, reaproveitada no Dashboard e nos Indicadores de Decisão. */
export function leituraMargemDre(margem: number | null): string {
  if (margem === null) return 'Sem receita no período para calcular a margem.'
  if (margem >= 0) return `De cada R$ 100 que entram, sobram R$ ${Math.round(margem)}.`
  return `De cada R$ 100 que entram, saem R$ ${Math.round(100 - margem)} — o negócio está no vermelho este mês.`
}

/** "Quanto preciso vender pra não ter prejuízo?" */
export function leituraPontoEquilibrio(pe: PontoEquilibrio): string {
  if (!pe.disponivel || pe.valorMensal === null) return pe.motivoIndisponivel ?? 'Sem dados suficientes para calcular.'
  const dist = pe.distancia ?? 0
  const pct = pe.distanciaPercentual !== null ? ` (${fmtPct(Math.abs(pe.distanciaPercentual))})` : ''
  if (dist >= 0) return `Faturando ${fmtBRL(pe.receitaAtual)}, você está ${fmtBRL(dist)}${pct} acima do ponto de equilíbrio.`
  return `Faturando ${fmtBRL(pe.receitaAtual)}, você está ${fmtBRL(Math.abs(dist))}${pct} abaixo do ponto de equilíbrio — operando no vermelho este mês.`
}

/** "Se parar de entrar dinheiro, quanto tempo eu duro?" */
export function leituraFolegoCaixa(f: FolegoCaixa): string {
  if (!f.disponivel || f.meses === null) return f.motivoIndisponivel ?? 'Sem dados suficientes para calcular.'
  const meses = f.meses.toFixed(1)
  if (f.faixa === 'critico') return `Você aguenta ${meses} mês(es) sem faturar nada. Abaixo de 1 mês é crítico: qualquer imprevisto zera o caixa rapidamente.`
  if (f.faixa === 'atencao') return `Você aguenta ${meses} meses sem faturar nada. Entre 1 e 3 meses dá pra reagir, mas sem muita margem de manobra.`
  return `Você aguenta ${meses} meses sem faturar nada. Acima de 3 meses é uma faixa confortável para se reorganizar diante de um problema.`
}

/** "Meu problema é faturamento ou é recebimento?" */
export function leituraPrazoRecebimento(p: PrazoRecebimento): string {
  if (!p.disponivel || p.mediaDias === null) return p.motivoIndisponivel ?? 'Sem dados suficientes para calcular.'
  if (Math.abs(p.mediaDias) < 1) return 'Em média, você recebe praticamente na data combinada — se há um problema de caixa, não é atraso no recebimento.'
  if (p.mediaDias > 0) return `Em média, você recebe ${p.mediaDias.toFixed(1)} dias depois da data combinada — parte do problema pode ser recebimento, não só faturamento.`
  return `Em média, você recebe ${Math.abs(p.mediaDias).toFixed(1)} dias antes da data combinada — bom sinal, não é o seu gargalo.`
}
