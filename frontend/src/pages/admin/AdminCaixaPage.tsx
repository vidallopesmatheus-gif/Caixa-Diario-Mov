import { useLocation } from 'react-router-dom'
import ClientCaixaPage from '../client/ClientCaixaPage'
import ClientHistoricoPage from '../client/ClientHistoricoPage'
import { useState } from 'react'

export default function AdminCaixaPage() {
  const location = useLocation()
  const clienteId = (location.state as any)?.clienteId ?? null
  const [tab, setTab] = useState<'caixa' | 'hist'>('caixa')

  if (!clienteId) return <p style={{ color: 'var(--tx3)' }}>Selecione um cliente na Visão Geral.</p>

  return (
    <>
      <div style={{ display: 'flex', gap: 8, marginBottom: 20 }}>
        <button className={`btn-sm${tab === 'caixa' ? ' btn-confirm' : ''}`} onClick={() => setTab('caixa')}>📋 Caixa</button>
        <button className={`btn-sm${tab === 'hist' ? ' btn-confirm' : ''}`} onClick={() => setTab('hist')}>📈 Histórico</button>
      </div>
      {tab === 'caixa'
        ? <ClientCaixaPage clienteIdOverride={clienteId} />
        : <ClientHistoricoPage clienteIdOverride={clienteId} />}
    </>
  )
}
