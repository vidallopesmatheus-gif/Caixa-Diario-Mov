import { fmtBRL } from '../../utils/format'
import './mv.css'

interface PayloadItem {
  name?: string
  value?: number | string
  color?: string
  dataKey?: string
}

interface Props {
  active?: boolean
  label?: string
  payload?: PayloadItem[]
  valueFormatter?: (v: number) => string
  /** Esconde séries com valor nulo/undefined (ex.: "realizado" nos pontos só-projetados). */
  ocultarVazios?: boolean
}

/** Tooltip padrão dos gráficos Mv* — fundo navy, borda glass, valores formatados em pt-BR. */
export default function MvTooltip({ active, label, payload, valueFormatter = fmtBRL, ocultarVazios = true }: Props) {
  if (!active || !payload || payload.length === 0) return null
  const linhas = payload.filter(p => !ocultarVazios || (p.value !== undefined && p.value !== null))
  if (linhas.length === 0) return null

  return (
    <div className="mv-chart-tooltip">
      {label && <div className="mv-chart-tooltip-label">{label}</div>}
      {linhas.map((p, i) => (
        <div key={i} className="mv-chart-tooltip-row">
          <span style={{ color: p.color ?? 'var(--tx2)' }}>{p.name}</span>
          <strong>{typeof p.value === 'number' ? valueFormatter(p.value) : String(p.value ?? '—')}</strong>
        </div>
      ))}
    </div>
  )
}
