import { useState, useEffect } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import StatCard from '../../components/shared/StatCard'
import { fmtBRL } from '../../utils/format'
import { obterEvolucao } from '../../api/metricas'
import type { EvolucaoMensal } from '../../api/metricas'
import {
  BarChart, Bar, LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip,
  ResponsiveContainer, Legend,
} from 'recharts'

interface Props { clienteIdOverride?: string }

const OPCOES_MESES = [3, 6, 12] as const

export default function ClientGraficoPage({ clienteIdOverride }: Props) {
  const { user } = useAuth()
  const clienteId = clienteIdOverride ?? user?.usuarioId ?? null
  const [meses, setMeses] = useState<3 | 6 | 12>(6)
  const [evolucao, setEvolucao] = useState<EvolucaoMensal[]>([])
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    if (!clienteId) return
    setLoading(true)
    obterEvolucao(clienteId, meses)
      .then(setEvolucao)
      .catch(console.error)
      .finally(() => setLoading(false))
  }, [clienteId, meses])

  const data = evolucao.map(e => ({
    mes: e.mes.slice(2),
    receita: e.receita,
    custos: e.custos,
    lucro: e.lucro,
    saldo: e.saldo,
  }))

  const totalReceita = evolucao.reduce((s, e) => s + e.receita, 0)
  const totalCustos = evolucao.reduce((s, e) => s + e.custos, 0)
  const totalLucro = evolucao.reduce((s, e) => s + e.lucro, 0)
  const saldoAtual = evolucao[evolucao.length - 1]?.saldo ?? 0

  if (loading) return <p style={{ color: 'var(--tx3)' }}>Carregando...</p>

  return (
    <>
      <div style={{ display: 'flex', gap: 8, marginBottom: 16 }}>
        {OPCOES_MESES.map(m => (
          <button key={m} onClick={() => setMeses(m)}
            style={{ padding: '6px 14px', borderRadius: 8, border: '1px solid var(--bd)', cursor: 'pointer',
              background: meses === m ? '#0a84ff' : 'var(--bg-card)', color: meses === m ? '#fff' : 'var(--tx1)', fontSize: 13 }}>
            {m} meses
          </button>
        ))}
      </div>

      <h3 style={{ fontSize: 15, fontWeight: 700, marginBottom: 14, color: '#888' }}>📊 Receita vs. Custos (barras)</h3>
      <div style={{ background: 'var(--bg-card)', border: '1px solid var(--bd)', borderRadius: 14, padding: 20, marginBottom: 24, height: 280 }}>
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={data}>
            <CartesianGrid strokeDasharray="3 3" stroke="var(--bd)" />
            <XAxis dataKey="mes" stroke="var(--tx3)" tick={{ fontSize: 12 }} />
            <YAxis stroke="var(--tx3)" tick={{ fontSize: 12 }} tickFormatter={v => `R$${(v/1000).toFixed(0)}k`} />
            <Tooltip formatter={(v) => typeof v === 'number' ? fmtBRL(v) : String(v)} contentStyle={{ background: 'var(--bg-card)', border: '1px solid var(--bd)' }} />
            <Legend />
            <Bar dataKey="receita" name="Receita" fill="#34c759" radius={[4,4,0,0]} />
            <Bar dataKey="custos" name="Custos" fill="#ff3b30" radius={[4,4,0,0]} />
          </BarChart>
        </ResponsiveContainer>
      </div>

      <h3 style={{ fontSize: 15, fontWeight: 700, marginBottom: 14, color: '#888' }}>📈 Lucro Operacional (linha)</h3>
      <div style={{ background: 'var(--bg-card)', border: '1px solid var(--bd)', borderRadius: 14, padding: 20, marginBottom: 24, height: 200 }}>
        <ResponsiveContainer width="100%" height="100%">
          <LineChart data={data}>
            <CartesianGrid strokeDasharray="3 3" stroke="var(--bd)" />
            <XAxis dataKey="mes" stroke="var(--tx3)" tick={{ fontSize: 12 }} />
            <YAxis stroke="var(--tx3)" tick={{ fontSize: 12 }} tickFormatter={v => `R$${(v/1000).toFixed(0)}k`} />
            <Tooltip formatter={(v) => typeof v === 'number' ? fmtBRL(v) : String(v)} contentStyle={{ background: 'var(--bg-card)', border: '1px solid var(--bd)' }} />
            <Line type="monotone" dataKey="lucro" name="Lucro Op." stroke="#ffd60a" strokeWidth={2} dot={{ fill: '#ffd60a' }} />
          </LineChart>
        </ResponsiveContainer>
      </div>

      <div className="stats-grid">
        <StatCard label="📈 Receita Total" value={fmtBRL(totalReceita)} className="val-green" />
        <StatCard label="💸 Custos Totais" value={fmtBRL(totalCustos)} className="val-red" />
        <StatCard label="📊 Lucro Total" value={fmtBRL(totalLucro)} className={totalLucro >= 0 ? 'val-green' : 'val-red'} />
        <StatCard label="💰 Saldo Atual" value={fmtBRL(saldoAtual)} className="val-blue" />
      </div>
    </>
  )
}
