import './mv.css'

interface Zona {
  /** Limite superior dessa zona de cor, na escala de `value` (cumulativo, em ordem crescente). */
  ate: number
  cor: string
}

interface Props {
  value: number
  min: number
  max: number
  /** Zonas de cor cumulativas (ex.: vermelho até 0, âmbar até 10, verde até 40). */
  zonas: Zona[]
  /** Marcador de referência opcional (ex.: limite de 60% em "posso assumir custo fixo novo?"). */
  marcador?: number
  valorFormatado: string
  legenda?: string
}

function polar(cx: number, cy: number, r: number, anguloDeg: number) {
  const rad = (anguloDeg * Math.PI) / 180
  return { x: cx + r * Math.cos(rad), y: cy - r * Math.sin(rad) }
}

function valorParaAngulo(v: number, min: number, max: number): number {
  const frac = Math.max(0, Math.min(1, (v - min) / (max - min)))
  return 180 * (1 - frac)
}

function arco(cx: number, cy: number, r: number, anguloInicio: number, anguloFim: number): string {
  const p1 = polar(cx, cy, r, anguloInicio)
  const p2 = polar(cx, cy, r, anguloFim)
  return `M ${p1.x} ${p1.y} A ${r} ${r} 0 0 1 ${p2.x} ${p2.y}`
}

/** Gauge em meia-lua com zonas de cor fixas + agulha — usado em qualquer indicador com faixas
 * "bom/atenção/ruim" (lucro de verdade, custo fixo ÷ receita, prazo médio de recebimento). */
export default function MvGauge({ value, min, max, zonas, marcador, valorFormatado, legenda }: Props) {
  const cx = 100, cy = 95, r = 76
  const angAgulha = valorParaAngulo(value, min, max)
  const pontaAgulha = polar(cx, cy, r - 18, angAgulha)

  // Ângulos cumulativos de cada zona, calculados numa passada só (sem mutar variável externa ao
  // longo do .map usado no JSX abaixo).
  const segmentos = zonas.reduce<{ cor: string; anguloInicio: number; anguloFim: number }[]>((acc, z) => {
    const anguloInicio = acc.length > 0 ? acc[acc.length - 1].anguloFim : 180
    const anguloFim = valorParaAngulo(Math.min(z.ate, max), min, max)
    acc.push({ cor: z.cor, anguloInicio, anguloFim })
    return acc
  }, [])

  return (
    <svg viewBox="0 0 200 115" width="100%" height={140} role="img" aria-label={legenda ?? valorFormatado}>
      <path d={arco(cx, cy, r, 180, 0)} fill="none" stroke="var(--bg-hover)" strokeWidth={16} strokeLinecap="round" />
      {segmentos.map((s, i) => (
        <path key={i} d={arco(cx, cy, r, s.anguloInicio, s.anguloFim)} fill="none" stroke={s.cor} strokeWidth={16} strokeLinecap="round" />
      ))}
      {marcador !== undefined && (() => {
        const ang = valorParaAngulo(marcador, min, max)
        const p1 = polar(cx, cy, r - 11, ang)
        const p2 = polar(cx, cy, r + 11, ang)
        return <line x1={p1.x} y1={p1.y} x2={p2.x} y2={p2.y} stroke="var(--tx1)" strokeWidth={2.5} />
      })()}
      <line x1={cx} y1={cy} x2={pontaAgulha.x} y2={pontaAgulha.y} stroke="var(--tx1)" strokeWidth={3} strokeLinecap="round" />
      <circle cx={cx} cy={cy} r={5} fill="var(--tx1)" />
      <text x={cx} y={cy - 20} textAnchor="middle" fontSize={22} fontWeight={700} fill="var(--tx1)">{valorFormatado}</text>
      {legenda && <text x={cx} y={cy - 2} textAnchor="middle" fontSize={10.5} fill="var(--tx2)">{legenda}</text>}
    </svg>
  )
}
