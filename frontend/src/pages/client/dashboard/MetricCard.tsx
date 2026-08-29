import Sparkline from '../../../components/shared/Sparkline'
import { fmtBRL, fmtPct } from '../../../utils/format'

interface Props {
  label: string
  valor: number | null
  variacaoPct: number | null
  comparativoLabel: string
  serie: number[]
  corPositivo?: boolean // se false, uma variação positiva é lida como ruim (ex: saídas)
}

export default function MetricCard({ label, valor, variacaoPct, comparativoLabel, serie, corPositivo = true }: Props) {
  const carregando = valor === null

  let variacaoClasse = ''
  let seta = ''
  if (variacaoPct !== null) {
    const bom = corPositivo ? variacaoPct >= 0 : variacaoPct <= 0
    variacaoClasse = variacaoPct === 0 ? '' : bom ? 'val-green' : 'val-red'
    seta = variacaoPct > 0 ? '▲' : variacaoPct < 0 ? '▼' : '—'
  }

  return (
    <div className="resumo-metric-card">
      <span className="resumo-metric-label">{label}</span>
      <span className={`resumo-metric-valor ${carregando ? 'resumo-skeleton' : ''}`}>
        {carregando ? '' : fmtBRL(valor)}
      </span>
      <div className="resumo-metric-rodape">
        <span className={`resumo-metric-variacao ${variacaoClasse}`}>
          {variacaoPct === null
            ? (carregando ? '' : `sem base de comparação`)
            : `${seta} ${fmtPct(Math.abs(variacaoPct))} ${comparativoLabel}`}
        </span>
        {serie.length >= 2 && (
          <Sparkline valores={serie} cor={variacaoClasse === 'val-red' ? '#ff3b30' : variacaoClasse === 'val-green' ? '#34c759' : undefined} />
        )}
      </div>
    </div>
  )
}
