import { useState } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import './ClientExportacao.css'

interface Props { clienteIdOverride?: string }

export default function ClientExportacaoPage({ clienteIdOverride }: Props) {
  const { user } = useAuth()
  const clienteId = clienteIdOverride ?? user?.usuarioId ?? null

  const hoje = new Date()
  const primeiroDia = `${hoje.getFullYear()}-${String(hoje.getMonth() + 1).padStart(2, '0')}-01`
  const ultimoDia = hoje.toISOString().slice(0, 10)

  const [de, setDe] = useState(primeiroDia)
  const [ate, setAte] = useState(ultimoDia)
  const [loadingXlsx, setLoadingXlsx] = useState(false)
  const [loadingPdf, setLoadingPdf] = useState(false)
  const [loadingCsv, setLoadingCsv] = useState(false)
  const [erro, setErro] = useState('')

  async function baixar(formato: 'xlsx' | 'pdf' | 'csv') {
    if (!clienteId) return
    setErro('')
    const setLoading = formato === 'xlsx' ? setLoadingXlsx : formato === 'pdf' ? setLoadingPdf : setLoadingCsv

    setLoading(true)
    try {
      const token = localStorage.getItem('token')
      const res = await fetch(`/api/export/${clienteId}/${formato}?de=${de}&ate=${ate}`, {
        headers: token ? { Authorization: `Bearer ${token}` } : {},
      })
      if (!res.ok) throw new Error(`Erro ${res.status}`)

      const blob = await res.blob()
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `relatorio_${de}_a_${ate}.${formato}`
      a.click()
      URL.revokeObjectURL(url)
    } catch (e: unknown) {
      setErro(e instanceof Error ? e.message : 'Erro ao exportar')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div style={{ maxWidth: 480, margin: '0 auto' }}>
      <h3 style={{ marginBottom: 20 }}>📥 Exportar Relatório</h3>

      <div style={{ background: 'var(--bg-card)', border: '1px solid var(--bd)', borderRadius: 14, padding: 20, marginBottom: 20 }}>
        <h4 style={{ marginBottom: 12, color: 'var(--tx3)', fontSize: 13, fontWeight: 600 }}>Período</h4>
        <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
          <label style={{ fontSize: 13 }}>De <input type="date" value={de} onChange={e => setDe(e.target.value)} /></label>
          <label style={{ fontSize: 13 }}>Até <input type="date" value={ate} onChange={e => setAte(e.target.value)} /></label>
        </div>
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
        <button onClick={() => baixar('xlsx')} disabled={loadingXlsx}
          style={{ padding: '14px 20px', borderRadius: 12, border: 'none', background: '#34c759', color: '#fff', fontSize: 15, fontWeight: 600, cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 8 }}>
          {loadingXlsx ? '⏳ Gerando...' : '📥 Baixar Excel (.xlsx)'}
        </button>

        <button onClick={() => baixar('pdf')} disabled={loadingPdf}
          style={{ padding: '14px 20px', borderRadius: 12, border: 'none', background: '#ff3b30', color: '#fff', fontSize: 15, fontWeight: 600, cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 8 }}>
          {loadingPdf ? '⏳ Gerando...' : '📄 Baixar PDF'}
        </button>

        <button onClick={() => baixar('csv')} disabled={loadingCsv}
          style={{ padding: '14px 20px', borderRadius: 12, border: 'none', background: '#0a84ff', color: '#fff', fontSize: 15, fontWeight: 600, cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 8 }}>
          {loadingCsv ? '⏳ Gerando...' : '📋 Baixar CSV'}
        </button>
      </div>

      {erro && <p style={{ marginTop: 12, color: '#ff3b30', fontSize: 13 }}>{erro}</p>}
    </div>
  )
}
