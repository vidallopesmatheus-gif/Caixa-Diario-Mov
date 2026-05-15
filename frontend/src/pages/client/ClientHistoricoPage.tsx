import { useAuth } from '../../contexts/AuthContext'
import { useRegistros } from '../../hooks/useRegistros'
import { fmtBRL, fmtDate, monthLabel } from '../../utils/format'
import StatCard from '../../components/shared/StatCard'
import { useState } from 'react'

interface Props { clienteIdOverride?: string }

export default function ClientHistoricoPage({ clienteIdOverride }: Props) {
  const { user } = useAuth()
  const clienteId = clienteIdOverride ?? user?.usuarioId ?? null
  const { registros, loading, excluir } = useRegistros(clienteId)
  const [openId, setOpenId] = useState<string | null>(null)
  const [delData, setDelData] = useState<string | null>(null)
  const [motivo, setMotivo] = useState('')

  const mesAtual = new Date().toISOString().slice(0, 7)
  const doMes = registros.filter(r => r.data.startsWith(mesAtual))
  const totalEnt = doMes.reduce((s, r) => s + r.entrada, 0)
  const totalSai = doMes.reduce((s, r) => s + r.saidas.reduce((a: number, x: any) => a + x.valor, 0), 0)
  const ultimo = doMes[0]?.saldoConfirmado ?? 0

  async function handleDelete() {
    if (!delData) return
    try { await excluir(delData, motivo); setDelData(null); setMotivo('') }
    catch (e: any) { alert(e.message) }
  }

  if (loading) return <p style={{ color: 'var(--tx3)' }}>Carregando...</p>

  return (
    <>
      <div className="stats-grid">
        <StatCard label="📅 Mês atual" value={monthLabel(mesAtual)} className="val-blue" />
        <StatCard label="📤 Total entradas" value={fmtBRL(totalEnt)} className="val-green" />
        <StatCard label="💸 Total saídas" value={fmtBRL(totalSai)} className="val-red" />
        <StatCard label="💰 Último saldo" value={fmtBRL(ultimo)} className="val-green" />
      </div>

      {registros.map(r => (
        <div key={r.id} className="hist-item" style={{ background: 'var(--bg-card)', border: '1px solid var(--bd)', borderRadius: 10, marginBottom: 8, overflow: 'hidden' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '12px 16px', cursor: 'pointer' }}
            onClick={() => setOpenId(openId === r.id ? null : r.id)}>
            <div>
              <div style={{ fontWeight: 600, fontSize: 14 }}>{fmtDate(r.data)}</div>
              <div style={{ fontSize: 12, color: 'var(--tx3)', marginTop: 2 }}>
                Entrada: {fmtBRL(r.entrada)} · Saídas: {fmtBRL(r.saidas.reduce((a: number, x: any) => a + x.valor, 0))}
              </div>
            </div>
            <div style={{ fontSize: 15, fontWeight: 700, color: '#34c759' }}>{fmtBRL(r.saldoConfirmado)}</div>
          </div>
          {openId === r.id && (
            <div style={{ padding: '12px 16px', borderTop: '1px solid var(--bd-sub)' }}>
              {r.saidas.map((s: any, i: number) => (
                <div key={i} style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13, padding: '4px 0', borderBottom: '1px solid var(--bg-card)' }}>
                  <span>{s.descricao}</span><span style={{ color: '#ff3b30' }}>-{fmtBRL(s.valor)}</span>
                </div>
              ))}
              <button style={{ marginTop: 8, background: 'none', border: '1px solid var(--bd)', borderRadius: 6, color: '#ff6b6b', fontSize: 12, padding: '3px 10px', cursor: 'pointer' }}
                onClick={() => setDelData(r.data)}>🗑 Excluir registro</button>
            </div>
          )}
        </div>
      ))}

      {delData && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,.7)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 100 }}>
          <div style={{ background: 'var(--bg-card)', borderRadius: 16, padding: 28, maxWidth: 400, width: '100%' }}>
            <div style={{ fontWeight: 700, fontSize: 17, marginBottom: 16 }}>Excluir registro de {delData}?</div>
            <div className="inp-group"><label>Motivo</label><input value={motivo} onChange={e => setMotivo(e.target.value)} placeholder="Informe o motivo" /></div>
            <div style={{ display: 'flex', gap: 10, marginTop: 16, justifyContent: 'flex-end' }}>
              <button className="btn-cancel" onClick={() => setDelData(null)}>Cancelar</button>
              <button className="btn-danger" onClick={handleDelete}>Excluir</button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}
