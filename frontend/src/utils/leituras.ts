/** Leitura textual simples da margem do DRE, reaproveitada no Dashboard e nos Indicadores de Decisão. */
export function leituraMargemDre(margem: number | null): string {
  if (margem === null) return 'Sem receita no período para calcular a margem.'
  if (margem >= 0) return `De cada R$ 100 que entram, sobram R$ ${Math.round(margem)}.`
  return `De cada R$ 100 que entram, saem R$ ${Math.round(100 - margem)} — o negócio está no vermelho este mês.`
}
