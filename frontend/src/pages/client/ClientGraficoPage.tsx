import { useAuth } from '../../contexts/AuthContext'
import { useRegistros } from '../../hooks/useRegistros'
import StatCard from '../../components/shared/StatCard'
import { fmtBRL, fmtDate } from '../../utils/format'
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts'

interface Props { clienteIdOverride?: string }

export default function ClientGraficoPage({ clienteIdOverride }: Props) {
  const { user } = useAuth()
  const clienteId = clienteIdOverride ?? user?.usuarioId ?? null
  const { registros, loading } = useRegistros(clienteId)

  const mesAtual = new Date().toISOString().slice(0, 7)
  const doMes = registros.filter(r => r.data.startsWith(mesAtual)).slice().reverse()

  const data = doMes.map(r => ({
    dia: fmtDate(r.data).slice(0, 5),
    saldo: r.saldoConfirmado,
  }))

  const primeiro = doMes[0]?.saldoInicio ?? 0
  const ultimo = doMes[doMes.length - 1]?.saldoConfirmado ?? 0
  const totalEnt = doMes.reduce((s, r) => s + r.entrada, 0)
  const totalSai = doMes.reduce((s, r) => s + r.saidas.reduce((a, x) => a + x.valor, 0), 0)

  if (loading) return <p style={{ color: 'var(--tx3)' }}>Carregando...</p>

  return (
    <>
      <h3 style={{ fontSize: 15, fontWeight: 700, marginBottom: 14, color: '#888' }}>📊 Evolução Mensal</h3>
      <div style={{ background: 'var(--bg-card)', border: '1px solid var(--bd)', borderRadius: 14, padding: 20, marginBottom: 24, height: 350 }}>
        <ResponsiveContainer width="100%" height="100%">
          <LineChart data={data}>
            <CartesianGrid strokeDasharray="3 3" stroke="var(--bd)" />
            <XAxis dataKey="dia" stroke="var(--tx3)" tick={{ fontSize: 12 }} />
            <YAxis stroke="var(--tx3)" tick={{ fontSize: 12 }} tickFormatter={v => fmtBRL(v).replace('R$ ', 'R$')} />
            <Tooltip formatter={(v) => typeof v === 'number' ? fmtBRL(v) : String(v)} contentStyle={{ background: 'var(--bg-card)', border: '1px solid var(--bd)' }} />
            <Line type="monotone" dataKey="saldo" stroke="#34c759" strokeWidth={2} dot={{ fill: '#34c759' }} />
          </LineChart>
        </ResponsiveContainer>
      </div>
      <div className="stats-grid">
        <StatCard label="💰 Saldo Inicial (Mês)" value={fmtBRL(primeiro)} className="val-blue" />
        <StatCard label="📈 Saldo Final (Mês)"   value={fmtBRL(ultimo)}   className="val-green" />
        <StatCard label="📤 Total Entradas"       value={fmtBRL(totalEnt)} className="val-green" />
        <StatCard label="💸 Total Saídas"         value={fmtBRL(totalSai)} className="val-red" />
      </div>
    </>
  )
}
