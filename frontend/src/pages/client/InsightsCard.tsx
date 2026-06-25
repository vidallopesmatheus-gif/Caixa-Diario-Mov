import { useState, useEffect } from 'react'
import { obterInsights } from '../../api/insights'
import type { Insight } from '../../api/insights'
import './InsightsCard.css'

interface Props { clienteId: string }

const ICONE: Record<string, string> = {
  alerta: '⚠️',
  positivo: '✅',
  neutro: 'ℹ️',
}

export default function InsightsCard({ clienteId }: Props) {
  const [insights, setInsights] = useState<Insight[]>([])
  const [expandido, setExpandido] = useState(false)
  const [carregado, setCarregado] = useState(false)

  useEffect(() => {
    if (!clienteId) return
    obterInsights(clienteId)
      .then(setInsights)
      .catch(() => {})
      .finally(() => setCarregado(true))
  }, [clienteId])

  if (!carregado || insights.length === 0) return null

  const visiveis = expandido ? insights : insights.slice(0, 3)
  const extras = insights.length - 3

  return (
    <div className="insights-card">
      <h3 className="insights-titulo">💡 Insights</h3>
      <div className="insights-lista">
        {visiveis.map((ins, i) => (
          <div key={i} className={`insight-item insight-${ins.tipo}`}>
            <span className="insight-icone">{ICONE[ins.tipo]}</span>
            <div className="insight-conteudo">
              <div className="insight-texto">{ins.texto}</div>
              {ins.detalhe && <div className="insight-detalhe">{ins.detalhe}</div>}
            </div>
          </div>
        ))}
      </div>
      {extras > 0 && (
        <button className="insights-ver-mais" onClick={() => setExpandido(e => !e)}>
          {expandido
            ? 'Ver menos'
            : `Ver mais ${extras} insight${extras > 1 ? 's' : ''}`}
        </button>
      )}
    </div>
  )
}
