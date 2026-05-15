import { useNavigate } from 'react-router-dom'
import { useUsuarios } from '../../hooks/useUsuarios'
import { useRegistros } from '../../hooks/useRegistros'
import StatCard from '../../components/shared/StatCard'
import { fmtBRL } from '../../utils/format'
import type { Usuario } from '../../types'
import './Admin.css'

function ClientCard({ usuario }: { usuario: Usuario }) {
  const { registros } = useRegistros(usuario.id)
  const navigate = useNavigate()
  const mesAtual = new Date().toISOString().slice(0, 7)
  const doMes = registros.filter(r => r.data.startsWith(mesAtual))
  const ultimo = registros[0]
  const totalEnt = doMes.reduce((s, r) => s + r.entrada, 0)
  const totalSai = doMes.reduce((s, r) => s + r.saidas.reduce((a, x) => a + x.valor, 0), 0)

  return (
    <div className="ov-card">
      <div>
        <div className="ov-name">{usuario.nomeCompleto}</div>
        <div className="ov-store">{usuario.nomeEstabelecimento}</div>
      </div>
      <div className="ov-saldo">{fmtBRL(ultimo?.saldoConfirmado ?? 0)}</div>
      <div className="ov-stats">
        <div className="ov-stat">
          <div className="ov-stat-lbl">Entradas (mês)</div>
          <div className="ov-stat-val val-green">{fmtBRL(totalEnt)}</div>
        </div>
        <div className="ov-stat">
          <div className="ov-stat-lbl">Saídas (mês)</div>
          <div className="ov-stat-val val-red">{fmtBRL(totalSai)}</div>
        </div>
      </div>
      <button className="btn-view-client"
        onClick={() => navigate('/admin/caixa', { state: { clienteId: usuario.id } })}>
        Ver caixa →
      </button>
    </div>
  )
}

export default function AdminOverviewPage() {
  const { usuarios, loading } = useUsuarios()
  const clientes = usuarios.filter(u => u.perfil === 'cliente' && u.ativo)

  if (loading) return <p style={{ color: 'var(--tx3)' }}>Carregando...</p>

  return (
    <>
      <div className="stats-grid">
        <StatCard label="👥 Total clientes" value={String(clientes.length)} />
      </div>
      <h3 style={{ fontSize: 15, fontWeight: 700, marginBottom: 14, color: '#888' }}>
        Clientes — Último saldo registrado
      </h3>
      <div className="overview-cards">
        {clientes.map(u => <ClientCard key={u.id} usuario={u} />)}
      </div>
    </>
  )
}
