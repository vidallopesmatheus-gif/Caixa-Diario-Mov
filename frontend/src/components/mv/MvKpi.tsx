import type { ReactNode } from 'react'
import Sparkline from '../shared/Sparkline'
import { fmtBRL } from '../../utils/format'
import type { MvTone } from './MvHero'
import './mv.css'

export function MvKpiGrid({ children }: { children: ReactNode }) {
  return <div className="mv-kpi-grid">{children}</div>
}

const SPARK_COR: Record<MvTone, string> = {
  good: 'var(--success)',
  bad: 'var(--danger)',
  warn: 'var(--warning)',
  neutral: 'var(--mv-blue-light)',
}

interface Props {
  label: string
  value: number | null
  formatter?: (n: number) => string
  tone?: MvTone
  /** Variação pronta pra exibir (ex.: "+12,3%"); passe tone condizente (good/bad). */
  delta?: string
  spark?: number[]
}

/** Card "de vidro" — valor grande + barra fina de cor na base + sparkline opcional. */
export default function MvKpi({ label, value, formatter = fmtBRL, tone = 'neutral', delta, spark }: Props) {
  return (
    <div className={`mv-kpi mv-tone-${tone}`}>
      <span className="mv-kpi-label">{label}</span>
      <div className="mv-kpi-valor-row">
        <span className="mv-kpi-valor">{value === null ? '—' : formatter(value)}</span>
        {delta && <span className="mv-kpi-delta" style={{ color: `var(--mv-kpi-tone)` }}>{delta}</span>}
      </div>
      {spark && spark.length >= 2 && (
        <div className="mv-kpi-spark"><Sparkline valores={spark} cor={SPARK_COR[tone]} /></div>
      )}
    </div>
  )
}
