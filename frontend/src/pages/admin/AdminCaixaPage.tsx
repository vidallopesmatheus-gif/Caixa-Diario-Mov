import { useParams } from 'react-router-dom'
import ClientCaixaPage from '../client/ClientCaixaPage'
import ClientHistoricoPage from '../client/ClientHistoricoPage'
import AdminAuditoriaPage from './AdminAuditoriaPage'
import { useState } from 'react'

export default function AdminCaixaPage() {
  const { clienteId } = useParams<{ clienteId: string }>()
  const [tab, setTab] = useState<'caixa' | 'hist' | 'auditoria'>('caixa')

  if (!clienteId) return <p style={{ color: 'var(--tx3)' }}>Selecione um cliente na Visão Geral.</p>

  return (
    <>
      <div style={{ display: 'flex', gap: 8, marginBottom: 20 }}>
        <button className={`btn-sm${tab === 'caixa' ? ' btn-confirm' : ''}`} onClick={() => setTab('caixa')}>📋 Caixa</button>
        <button className={`btn-sm${tab === 'hist' ? ' btn-confirm' : ''}`} onClick={() => setTab('hist')}>📈 Histórico</button>
        <button className={`btn-sm${tab === 'auditoria' ? ' btn-confirm' : ''}`} onClick={() => setTab('auditoria')}>🔍 Auditoria</button>
      </div>
      {tab === 'caixa' && <ClientCaixaPage clienteIdOverride={clienteId} />}
      {tab === 'hist' && <ClientHistoricoPage clienteIdOverride={clienteId} />}
      {tab === 'auditoria' && <AdminAuditoriaPage clienteId={clienteId} />}
    </>
  )
}

