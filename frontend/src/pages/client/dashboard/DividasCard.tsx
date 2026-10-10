import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { listarFinanciamentos } from '../../../api/financiamentos'
import { fmtBRL } from '../../../utils/format'
import type { Financiamento } from '../../../types'

interface Props { clienteId: string }

/** Fase 6H: resumo de financiamentos/empréstimos/consórcios/parcelamentos no Dashboard — só
 * aparece quando o cliente tem algum contrato ativo (card vazio não ajuda ninguém). */
export default function DividasCard({ clienteId }: Props) {
  const navigate = useNavigate()
  const [financiamentos, setFinanciamentos] = useState<Financiamento[] | null>(null)

  useEffect(() => {
    listarFinanciamentos(clienteId).then(setFinanciamentos).catch(() => setFinanciamentos([]))
  }, [clienteId])

  if (!financiamentos) return null
  const ativos = financiamentos.filter(f => f.status === 'Ativo')
  if (ativos.length === 0) return null

  const saldoDevedorTotal = ativos.reduce((s, f) => s + f.saldoDevedor, 0)
  const parcelaMensalComprometida = ativos.reduce((s, f) => s + (f.proximaParcelaValor ?? 0), 0)

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
    </div>
  )
}
