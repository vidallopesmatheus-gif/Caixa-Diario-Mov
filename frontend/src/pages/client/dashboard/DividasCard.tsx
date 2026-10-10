import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { listarFinanciamentos } from '../../../api/financiamentos'
import { obterEvolucao } from '../../../api/metricas'
import { fmtBRL, fmtPct } from '../../../utils/format'
import type { Financiamento } from '../../../types'

interface Props { clienteId: string }

// Item B5: verde < 15%, amarelo 15–30%, vermelho > 30% de comprometimento da receita média.
function corComprometimento(pct: number): string {
  if (pct < 15) return 'var(--success)'
  if (pct <= 30) return 'var(--warning)'
  return 'var(--danger)'
}

/** Fase 6H: resumo de financiamentos/empréstimos/consórcios/parcelamentos no Dashboard — só
 * aparece quando o cliente tem algum contrato ativo (card vazio não ajuda ninguém). */
export default function DividasCard({ clienteId }: Props) {
  const navigate = useNavigate()
  const [financiamentos, setFinanciamentos] = useState<Financiamento[] | null>(null)
  const [receitaMedia, setReceitaMedia] = useState<number | null>(null)

  useEffect(() => {
    listarFinanciamentos(clienteId).then(setFinanciamentos).catch(() => setFinanciamentos([]))
    // 4 meses e descarta o atual (parcial) — "últimos 3 meses" são os 3 meses fechados anteriores.
    obterEvolucao(clienteId, 4)
      .then(evolucao => {
        const anteriores = evolucao.slice(0, -1)
        const media = anteriores.length > 0 ? anteriores.reduce((s, e) => s + e.receita, 0) / anteriores.length : 0
        setReceitaMedia(media)
      })
      .catch(() => setReceitaMedia(0))
  }, [clienteId])

  if (!financiamentos) return null
  const ativos = financiamentos.filter(f => f.status === 'Ativo')
  if (ativos.length === 0) return null

  const saldoDevedorTotal = ativos.reduce((s, f) => s + f.saldoDevedor, 0)
  const parcelaMensalComprometida = ativos.reduce((s, f) => s + (f.proximaParcelaValor ?? 0), 0)
  const pctReceita = receitaMedia && receitaMedia > 0 ? (parcelaMensalComprometida / receitaMedia) * 100 : null

  return (
    <div className="meta-card" style={{ cursor: 'pointer' }} onClick={() => navigate('/contas/financiamentos')}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <strong>💳 Dívidas</strong>
        <span style={{ color: 'var(--tx3)', fontSize: 12 }}>{ativos.length} contrato(s) ativo(s)</span>
      </div>
      <div style={{ display: 'flex', gap: 24, marginTop: 10, flexWrap: 'wrap' }}>
        <div>
          <span style={{ fontSize: 12, color: 'var(--tx3)' }}>Saldo devedor total</span>
          <div style={{ fontSize: 18, fontWeight: 700 }}>{fmtBRL(saldoDevedorTotal)}</div>
        </div>
        <div>
          <span style={{ fontSize: 12, color: 'var(--tx3)' }}>Parcela mensal comprometida</span>
          <div style={{ fontSize: 18, fontWeight: 700 }}>{fmtBRL(parcelaMensalComprometida)}</div>
        </div>
      </div>
      {pctReceita != null && (
        <div style={{ marginTop: 10, fontSize: 13 }}>
          Parcelas = <strong style={{ color: corComprometimento(pctReceita) }}>{fmtPct(pctReceita)}</strong> da receita média dos últimos 3 meses
        </div>
      )}
    </div>
  )
}
