import { useState, useEffect } from 'react'
import { PieChart, Pie } from 'recharts'
import { obterSaudeFinanceira } from '../../api/saudeFinanceira'
import type { GaugeIndicador, SaudeFinanceira } from '../../api/saudeFinanceira'
import './SaudeFinanceiraGauges.css'

interface Props { clienteId: string }

const COR: Record<string, string> = {
  verde:    '#059669',
  amarelo:  '#D97706',
  vermelho: '#DC2626',
  cinza:    '#6B7280',
}

// Metadata por gauge: unidade de display e se "maior é melhor" (para legenda)
const META: Record<string, { unidade: string; maiorMelhor: boolean }> = {
  taxaPoupanca:        { unidade: '%', maiorMelhor: true },
  comprometimentoFixos:{ unidade: '%', maiorMelhor: false },
  ritmoMeta:           { unidade: '%', maiorMelhor: true },
}

interface GaugeProps {
  id: string
  dado: GaugeIndicador
}

function Gauge({ id, dado }: GaugeProps) {
  const [hovered, setHovered] = useState(false)
  const { unidade, maiorMelhor } = META[id]
  const cor = COR[dado.semaforo]
  const pct = Math.max(0, Math.min(100, dado.valorNormalizado))

  // Semicírculo: startAngle=180 endAngle=0 — arco superior de 180°
  const arcData = [
    { value: pct,       fill: cor },
    { value: 100 - pct, fill: 'rgba(120,120,128,.13)' },
  ]

  const displayValor = dado.disponivel
    ? `${dado.valor > 100 ? '>' : ''}${Math.min(999, Math.round(dado.valor))}${unidade}`
    : '–'

  // Legenda de faixas coloridas (vermelho → amarelo → verde, ou inverso)
  const faixas = maiorMelhor
    ? ['#DC2626', '#D97706', '#059669']
    : ['#059669', '#D97706', '#DC2626']

  return (
    <div
      className="gauge-item"
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
    >
      {hovered && dado.disponivel && (
        <div className="gauge-tooltip">
          <p className="gauge-tip-desc">{dado.descricao}</p>
          <p className="gauge-tip-calculo">{dado.calculo}</p>
        </div>
      )}

      {/* SVG semicircle via Recharts */}
      <div className="gauge-arc">
        <PieChart width={150} height={88}>
          <Pie
            data={arcData}
            cx={75}
            cy={82}
            startAngle={180}
            endAngle={0}
            innerRadius={48}
            outerRadius={68}
            dataKey="value"
            strokeWidth={0}
            isAnimationActive={false}
          />
        </PieChart>
      </div>

      <div className="gauge-bottom">
        <span className="gauge-valor" style={{ color: dado.disponivel ? cor : '#6B7280' }}>
          {displayValor}
        </span>
        <span className="gauge-nome">{dado.titulo}</span>
        <div className="gauge-faixas">
          {faixas.map((c, i) => (
            <div key={i} className="gauge-faixa" style={{ background: c }} />
          ))}
        </div>
      </div>
    </div>
  )
}

export default function SaudeFinanceiraGauges({ clienteId }: Props) {
  const [dados, setDados] = useState<SaudeFinanceira | null>(null)

  useEffect(() => {
    if (!clienteId) return
    obterSaudeFinanceira(clienteId)
      .then(setDados)
      .catch(() => {})
  }, [clienteId])

  if (!dados) return null

  return (
    <div className="saude-card">
      <h3 className="saude-titulo">🩺 Saúde Financeira</h3>
      <div className="saude-gauges">
        <Gauge id="taxaPoupanca"         dado={dados.taxaPoupanca} />
        <Gauge id="comprometimentoFixos" dado={dados.comprometimentoFixos} />
        <Gauge id="ritmoMeta"            dado={dados.ritmoMeta} />
      </div>
    </div>
  )
}
