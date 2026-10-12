import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { obterMeuFuturo } from '../../../api/projecao'
import { obterPreferenciasFuturo } from '../../../api/preferenciasFuturo'
import { fmtBRL, monthLabel } from '../../../utils/format'
import type { MeuFuturo } from '../../../api/projecao'

interface Props { clienteId: string }

const STATUS_COR: Record<string, string> = { Tranquilo: 'var(--success)', Atencao: 'var(--warning)', Risco: 'var(--danger)' }
const STATUS_LABEL: Record<string, string> = { Tranquilo: 'tranquilo', Atencao: 'atenção', Risco: 'risco' }

// Bloco 3 da rodada "Visão de Futuro": versão resumida (1 frase + o pior mês) da tela "Meu
// Futuro" completa, linkando pra ela — mesmo padrão de DividasCard.
export default function MeuFuturoCard({ clienteId }: Props) {
  const navigate = useNavigate()
  const [meuFuturo, setMeuFuturo] = useState<MeuFuturo | null>(null)

  useEffect(() => {
    obterPreferenciasFuturo(clienteId)
      .then(prefs => obterMeuFuturo(clienteId, { reservaMinima: prefs.reservaMinima }))
      .then(setMeuFuturo)
      .catch(() => setMeuFuturo(null))
  }, [clienteId])

  if (!meuFuturo || meuFuturo.meses.length === 0) return null

  const piorMes = meuFuturo.meses.reduce((pior, m) => m.saldoFinal < pior.saldoFinal ? m : pior, meuFuturo.meses[0])

  return (
    <div className="meta-card" style={{ cursor: 'pointer' }} onClick={() => navigate('/resultados/futuro')}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <strong>🔮 Meu Futuro</strong>
        <span style={{ color: STATUS_COR[piorMes.status], fontSize: 12, fontWeight: 700 }}>
          Pior mês: {STATUS_LABEL[piorMes.status]}
        </span>
      </div>
      <div style={{ marginTop: 10, fontSize: 13, color: 'var(--tx2)' }}>
        {piorMes.status === 'Tranquilo'
          ? 'Seu caixa deve seguir saudável nos próximos 12 meses.'
          : <>Em <strong>{monthLabel(piorMes.mes)}</strong>, o saldo projetado é <strong style={{ color: STATUS_COR[piorMes.status] }}>{fmtBRL(piorMes.saldoFinal)}</strong>.</>}
      </div>
    </div>
  )
}
