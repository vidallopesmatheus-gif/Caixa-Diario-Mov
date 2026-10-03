import { useState, useEffect } from 'react'
import { PieChart, Pie } from 'recharts'
import { obterSaudeFinanceira } from '../../api/saudeFinanceira'
import type { GaugeIndicador, SaudeFinanceira } from '../../api/saudeFinanceira'
import { fmtBRL } from '../../utils/format'
import './SaudeFinanceiraGauges.css'

interface Props { clienteId: string }

const COR: Record<string, string> = {
  verde:    '#059669',
  amarelo:  '#D97706',
  vermelho: '#DC2626',
  cinza:    '#6B7280',
}

// Metadata por gauge: unidade de display e a legenda das faixas de cor (mesmos limiares usados
// no backend, SaudeFinanceiraService.cs) — texto em vez de bolinhas coloridas sem explicação.
const META: Record<string, { unidade: string; legenda: string }> = {
  taxaPoupanca:        { unidade: '%', legenda: 'Saudável: 20%+ · Atenção: 5–20% · Baixo: abaixo de 5%' },
  comprometimentoFixos:{ unidade: '%', legenda: 'Saudável: até 40% · Atenção: 40–70% · Alto: acima de 70%' },
  ritmoMeta:           { unidade: '%', legenda: 'No ritmo: 90%+ do esperado · Atenção: 70–90% · Atrasado: abaixo de 70%' },
}

const STATUS_RITMO_LABEL: Record<string, string> = {
  Atingida: '🎉 Meta atingida',
  Adiantado: '🟢 Adiantado',
  'No ritmo': '🟡 No ritmo',
  Atrasado: '🔴 Atrasado',
}

interface GaugeProps {
  id: string
  dado: GaugeIndicador
}

function Gauge({ id, dado }: GaugeProps) {
  const [hovered, setHovered] = useState(false)
  const { unidade, legenda } = META[id]
  const cor = COR[dado.semaforo]
  const pct = Math.max(0, Math.min(100, dado.valorNormalizado))

  // Semicírculo: startAngle=180 endAngle=0 — arco superior de 180°
  const arcData = [
    { value: pct,       fill: cor },
    { value: 100 - pct, fill: 'rgba(120,120,128,.13)' },
  ]

  const displayValor = dado.disponivel
    ? (id === 'ritmoMeta' && dado.statusRitmo
        ? (STATUS_RITMO_LABEL[dado.statusRitmo] ?? dado.statusRitmo)
        : `${dado.valor > 100 ? '>' : ''}${Math.min(999, Math.round(dado.valor))}${unidade}`)
    : '–'

  return (
    <div
      className="gauge-item"
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
    >
      {hovered && (
        <div className="gauge-tooltip">
          <p className="gauge-tip-desc">{dado.descricao}</p>
          <p className="gauge-tip-calculo">{dado.calculo}</p>
          {/* Faixas de referência em texto — antes eram só 3 bolinhas coloridas sem nenhuma
              explicação do que cada uma significava. */}
          <p className="gauge-tip-legenda">{legenda}</p>
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
        {dado.disponivel && id === 'ritmoMeta' && typeof dado.diferencaReais === 'number' && dado.statusRitmo !== 'Atingida' && (
          <span className="gauge-motivo">
            {dado.diferencaReais >= 0
              ? `${fmtBRL(dado.diferencaReais)} à frente do esperado`
              : `${fmtBRL(Math.abs(dado.diferencaReais))} atrás do esperado`}
          </span>
        )}
        {!dado.disponivel && (
          <span className="gauge-motivo">
            {dado.calculo}
            {id === 'ritmoMeta' && dado.calculo.includes('Nenhuma meta') && (
              <> <a href="#metas-investimentos">cadastrar →</a></>
            )}
          </span>
        )}
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
      <h3 className="saude-titulo">
        🩺 Saúde Financeira
        {/* Taxa de Poupança e Comprometimento Fixo usam sempre o último mês INTEIRO já fechado
            (não o corrente, ainda incompleto), independente do período escolhido lá em cima no
            Dashboard — deixar isso explícito evita o "sem receita" sem contexto no dia 1. */}
        <span className="saude-titulo-periodo">{dados.periodo}</span>
      </h3>
      <div className="saude-gauges">
        <Gauge id="taxaPoupanca"         dado={dados.taxaPoupanca} />
        <Gauge id="comprometimentoFixos" dado={dados.comprometimentoFixos} />
        <Gauge id="ritmoMeta"            dado={dados.ritmoMeta} />
      </div>
    </div>
  )
}
