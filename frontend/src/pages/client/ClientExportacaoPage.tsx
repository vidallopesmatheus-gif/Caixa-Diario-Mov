import { useState } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import './ClientExportacao.css'

interface Props { clienteIdOverride?: string }

export default function ClientExportacaoPage({ clienteIdOverride }: Props) {
  const { user } = useAuth()
  const clienteId = clienteIdOverride ?? user?.usuarioId ?? null

  const hoje = new Date()
  const primeiroDia = `${hoje.getFullYear()}-${String(hoje.getMonth() + 1).padStart(2,'0')}-01`
  const ultimoDia = hoje.toISOString().slice(0,10)

  const [de, setDe] = useState(primeiroDia)
  const [ate, setAte] = useState(ultimoDia)
  const [loading, setLoading] = useState(false)
  const [erro, setErro] = useState('')

  async function handleExportar() {
    if (!clienteId) return
    setLoading(true)
    setErro('')
    try {
      const token = localStorage.getItem('token') ?? ''
      const res = await fetch(`/api/export/${clienteId}?de=${de}&ate=${ate}`, {
        headers: { Authorization: `Bearer ${token}` }
      })
      if (!res.ok) {
        const json = await res.json().catch(() => ({}))
        throw new Error((json as { mensagem?: string })?.mensagem ?? `Erro ${res.status}`)
      }
      const blob = await res.blob()
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `relatorio_${de}_a_${ate}.xlsx`
      a.click()
      URL.revokeObjectURL(url)
    } catch (e: unknown) {
      setErro(e instanceof Error ? e.message : String(e))
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="export-card">
      <h3>📊 Exportar para Excel</h3>
      <div className="export-period">
        <label>
          Data inicial
          <input type="date" value={de} onChange={e => setDe(e.target.value)} />
        </label>
        <label>
          Data final
          <input type="date" value={ate} onChange={e => setAte(e.target.value)} max={ultimoDia} />
        </label>
      </div>
      <button className="btn-export" onClick={handleExportar} disabled={loading || !de || !ate || ate < de}>
        {loading ? 'Gerando...' : '⬇️ Baixar Excel'}
      </button>
      {erro && <div style={{ marginTop: 10, color: '#ff6b6b', fontSize: 13 }}>{erro}</div>}
      <p className="export-info">
        O arquivo inclui todas as entradas, saídas, lucro operacional e saldo final, dia a dia, no período selecionado.
      </p>
    </div>
  )
}
