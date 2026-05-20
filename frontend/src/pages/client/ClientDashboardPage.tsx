import { useState, useEffect, useMemo } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { useRegistros } from '../../hooks/useRegistros'
import StatCard from '../../components/shared/StatCard'
import { fmtBRL } from '../../utils/format'
import { obterMeta, salvarMeta } from '../../api/metas'
import type { MetaAnual } from '../../types'
import './ClientDashboard.css'

interface Props { clienteIdOverride?: string }

const MONTHS = ['Jan','Fev','Mar','Abr','Mai','Jun','Jul','Ago','Set','Out','Nov','Dez']

export default function ClientDashboardPage({ clienteIdOverride }: Props) {
  const { user } = useAuth()
  const clienteId = clienteIdOverride ?? user?.usuarioId ?? null
  const { registros, loading } = useRegistros(clienteId)

  const hoje = new Date()
  const anoAtual = hoje.getFullYear()
  const mesAtual = hoje.getMonth() + 1

  const primeiroDiaMes = `${anoAtual}-${String(mesAtual).padStart(2,'0')}-01`
  const ultimoDiaMes = new Date(anoAtual, mesAtual, 0).toISOString().slice(0,10)

  const [de, setDe] = useState(primeiroDiaMes)
  const [ate, setAte] = useState(ultimoDiaMes)
  const [meta, setMeta] = useState<MetaAnual | null>(null)
  const [editReceita, setEditReceita] = useState('')
  const [editLucro, setEditLucro] = useState('')
  const [savingMeta, setSavingMeta] = useState(false)
  const [metaMsg, setMetaMsg] = useState('')

  useEffect(() => {
    if (!clienteId) return
    obterMeta(clienteId, anoAtual)
      .then(m => {
        setMeta(m)
        if (m) { setEditReceita(String(m.metaReceita)); setEditLucro(String(m.metaLucro)) }
      })
      .catch(console.error)
  }, [clienteId, anoAtual])

  const doPeriodo = useMemo(() =>
    registros.filter(r => r.data >= de && r.data <= ate),
    [registros, de, ate]
  )

  const totalReceita = doPeriodo.reduce((s, r) => s + r.entradas.reduce((a, e) => a + e.valor, 0), 0)
  const totalSaida = doPeriodo.reduce((s, r) => s + r.saidas.reduce((a, e) => a + e.valor, 0), 0)
  const lucroOp = totalReceita - totalSaida
  const saldoFinal = doPeriodo[doPeriodo.length - 1]?.saldoConfirmado ?? 0

  const planejamento = useMemo(() => {
    if (!meta) return []
    let remainingReceita = meta.metaReceita
    let remainingLucro = meta.metaLucro

    return MONTHS.map((label, i) => {
      const mes = i + 1
      const remainingMonths = 12 - mes + 1
      const targetReceita = remainingReceita / remainingMonths
      const targetLucro = remainingLucro / remainingMonths

      const prefixo = `${anoAtual}-${String(mes).padStart(2,'0')}`
      const doMes = registros.filter(r => r.data.startsWith(prefixo))
      const receitaReal = doMes.reduce((s, r) => s + r.entradas.reduce((a, e) => a + e.valor, 0), 0)
      const lucroReal = doMes.reduce((s, r) => s + r.entradas.reduce((a,e) => a+e.valor,0) - r.saidas.reduce((a,e) => a+e.valor,0), 0)

      const isPassado = mes < mesAtual
      const isAtual = mes === mesAtual

      if (isPassado || isAtual) {
        remainingReceita -= receitaReal
        remainingLucro -= lucroReal
      }

      return {
        mes, label: `${label}/${anoAtual}`, targetReceita, targetLucro,
        receitaReal: (isPassado || isAtual) ? receitaReal : null,
        lucroReal: (isPassado || isAtual) ? lucroReal : null,
        isAtual,
      }
    })
  }, [meta, registros, anoAtual, mesAtual])

  async function handleSaveMeta() {
    if (!clienteId) return
    setSavingMeta(true)
    setMetaMsg('')
    try {
      const saved = await salvarMeta({ clienteId, ano: anoAtual, metaReceita: Number(editReceita), metaLucro: Number(editLucro) })
      setMeta(saved)
      setMetaMsg('Meta salva!')
    } catch (e: unknown) {
      setMetaMsg(e instanceof Error ? e.message : String(e))
    } finally {
      setSavingMeta(false)
    }
  }

  if (loading) return <p style={{ color: 'var(--tx3)' }}>Carregando...</p>

  return (
    <>
      <div className="dash-period">
        <label>De <input type="date" value={de} onChange={e => setDe(e.target.value)} /></label>
        <label>Até <input type="date" value={ate} onChange={e => setAte(e.target.value)} /></label>
      </div>

      <div className="stats-grid">
        <StatCard label="📈 Total Receita" value={fmtBRL(totalReceita)} className="val-green" />
        <StatCard label="💸 Total Saída" value={fmtBRL(totalSaida)} className="val-red" />
        <StatCard label="📊 Lucro Operacional" value={fmtBRL(lucroOp)} className={lucroOp >= 0 ? 'val-green' : 'val-red'} />
        <StatCard label="💰 Saldo Final" value={fmtBRL(saldoFinal)} className="val-blue" />
      </div>

      <div className="meta-card">
        <h3>🎯 Metas Anuais {anoAtual}</h3>
        <div className="meta-row">
          <label>Meta de Receita Anual (R$)</label>
          <input type="number" value={editReceita} onChange={e => setEditReceita(e.target.value)} placeholder="Ex: 120000" min="0" step="0.01" />
        </div>
        <div className="meta-row">
          <label>Meta de Lucro Anual (R$)</label>
          <input type="number" value={editLucro} onChange={e => setEditLucro(e.target.value)} placeholder="Ex: 60000" min="0" step="0.01" />
        </div>
        <button className="btn-meta" onClick={handleSaveMeta} disabled={savingMeta}>
          {savingMeta ? 'Salvando...' : '💾 Salvar metas'}
        </button>
        {metaMsg && <span style={{ marginLeft: 12, fontSize: 13, color: '#34c759' }}>{metaMsg}</span>}
      </div>

      {meta && planejamento.length > 0 && (
        <div className="meta-card">
          <h3>📅 Projeção Mês a Mês — {anoAtual}</h3>
          <p style={{ fontSize: 12, color: 'var(--tx3)', marginBottom: 10 }}>
            Se não bater a meta em um mês, o sistema redistribui o restante nos meses seguintes.
          </p>
          <table className="plan-table">
            <thead>
              <tr>
                <th>Mês</th>
                <th>Meta Receita</th>
                <th>Receita Real</th>
                <th>Meta Lucro</th>
                <th>Lucro Real</th>
              </tr>
            </thead>
            <tbody>
              {planejamento.map(p => (
                <tr key={p.mes} className={p.isAtual ? 'atual' : ''}>
                  <td>{p.label}</td>
                  <td>{fmtBRL(p.targetReceita)}</td>
                  <td>
                    {p.receitaReal !== null
                      ? <span className={p.receitaReal >= p.targetReceita ? 'ok' : 'nok'}>{fmtBRL(p.receitaReal)}</span>
                      : <span className="proj">—</span>}
                  </td>
                  <td>{fmtBRL(p.targetLucro)}</td>
                  <td>
                    {p.lucroReal !== null
                      ? <span className={p.lucroReal >= p.targetLucro ? 'ok' : 'nok'}>{fmtBRL(p.lucroReal)}</span>
                      : <span className="proj">—</span>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  )
}
