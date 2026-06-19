import { useState, useMemo, useEffect } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { useRegistros } from '../../hooks/useRegistros'
import { fmtBRL, fmtDate, todayISO } from '../../utils/format'
import { listarContasRecorrentes, criarContaRecorrente, desativarContaRecorrente } from '../../api/contasRecorrentes'
import type { ContaProvisionada, ContaRecorrente } from '../../types'
import './ClientContas.css'

interface Props { clienteIdOverride?: string }

interface ContaView {
  registroData: string
  tipo: 'receber' | 'pagar'
  index: number
  conta: ContaProvisionada
}

export default function ClientContasPage({ clienteIdOverride }: Props) {
  const { user } = useAuth()
  const clienteId = clienteIdOverride ?? user?.usuarioId ?? null
  const { registros, salvar, loading } = useRegistros(clienteId)

  const [novaDesc, setNovaDesc] = useState('')
  const [novoValor, setNovoValor] = useState('')
  const [novoVenc, setNovoVenc] = useState('')
  const [novoTipo, setNovoTipo] = useState<'receber' | 'pagar'>('receber')
  const [saving, setSaving] = useState(false)
  const [msg, setMsg] = useState('')

  const [recorrentes, setRecorrentes] = useState<ContaRecorrente[]>([])
  const [novaRecDesc, setNovaRecDesc] = useState('')
  const [novaRecValor, setNovaRecValor] = useState('')
  const [novaRecTipo, setNovaRecTipo] = useState<'Receber' | 'Pagar'>('Pagar')
  const [novaRecInicio, setNovaRecInicio] = useState('')
  const [novaRecFim, setNovaRecFim] = useState('')
  const [novaRecPeriodicidade, setNovaRecPeriodicidade] = useState('Mensal')
  const [novaRecParcelas, setNovaRecParcelas] = useState('')
  const [savingRec, setSavingRec] = useState(false)

  useEffect(() => {
    if (!clienteId) return
    listarContasRecorrentes(clienteId).then(setRecorrentes).catch(console.error)
  }, [clienteId])

  async function adicionarRecorrente() {
    if (!clienteId || !novaRecDesc || !novaRecValor || !novaRecInicio) return
    setSavingRec(true)
    try {
      const nova = await criarContaRecorrente({
        clienteId, descricao: novaRecDesc, valor: Number(novaRecValor),
        tipo: novaRecTipo, dataInicio: novaRecInicio, dataFim: novaRecFim || undefined,
        periodicidade: novaRecPeriodicidade,
        quantidadeParcelas: novaRecParcelas ? Number(novaRecParcelas) : undefined,
      })
      setRecorrentes(prev => [...prev, nova])
      setNovaRecDesc(''); setNovaRecValor(''); setNovaRecInicio(''); setNovaRecFim('')
      setNovaRecPeriodicidade('Mensal'); setNovaRecParcelas('')
    } finally {
      setSavingRec(false)
    }
  }

  async function handleDesativar(id: string) {
    if (!clienteId) return
    await desativarContaRecorrente(clienteId, id)
    setRecorrentes(prev => prev.filter(r => r.id !== id))
  }

  const todasContas = useMemo<ContaView[]>(() => {
    const acc: ContaView[] = []
    for (const reg of registros) {
      reg.contasAReceber.forEach((c, i) => acc.push({ registroData: reg.data, tipo: 'receber', index: i, conta: c }))
      reg.contasAPagar.forEach((c, i) => acc.push({ registroData: reg.data, tipo: 'pagar', index: i, conta: c }))
    }
    return acc.sort((a, b) => {
      const da = a.conta.dataVencimento ?? a.registroData
      const db = b.conta.dataVencimento ?? b.registroData
      return da.localeCompare(db)
    })
  }, [registros])

  const pendentesReceber = todasContas.filter(c => c.tipo === 'receber' && !c.conta.pago)
  const recebidasList = todasContas.filter(c => c.tipo === 'receber' && c.conta.pago)
  const pendentesPagar = todasContas.filter(c => c.tipo === 'pagar' && !c.conta.pago)
  const pagasList = todasContas.filter(c => c.tipo === 'pagar' && c.conta.pago)

  async function togglePago(view: ContaView) {
    if (!clienteId) return
    const reg = registros.find(r => r.data === view.registroData)
    if (!reg) return
    await salvar({
      clienteId,
      data: reg.data,
      saldoInicio: reg.saldoInicio,
      entradas: reg.entradas,
      saidas: reg.saidas,
      contasAReceber: reg.contasAReceber.map((c, i) => view.tipo === 'receber' && i === view.index ? { ...c, pago: !c.pago } : c),
      contasAPagar: reg.contasAPagar.map((c, i) => view.tipo === 'pagar' && i === view.index ? { ...c, pago: !c.pago } : c),
      saldoConfirmado: reg.saldoConfirmado,
    })
  }

  async function adicionarConta() {
    if (!clienteId || !novaDesc || !novoValor) return
    setSaving(true)
    setMsg('')
    try {
      const hoje = todayISO()
      const reg = registros.find(r => r.data === hoje)
      const novaConta: ContaProvisionada = { descricao: novaDesc, valor: Number(novoValor), dataVencimento: novoVenc || undefined, pago: false }
      await salvar({
        clienteId,
        data: hoje,
        saldoInicio: reg?.saldoInicio ?? 0,
        entradas: reg?.entradas ?? [],
        saidas: reg?.saidas ?? [],
        contasAReceber: novoTipo === 'receber' ? [...(reg?.contasAReceber ?? []), novaConta] : (reg?.contasAReceber ?? []),
        contasAPagar: novoTipo === 'pagar' ? [...(reg?.contasAPagar ?? []), novaConta] : (reg?.contasAPagar ?? []),
        saldoConfirmado: reg?.saldoConfirmado ?? 0,
      })
      setNovaDesc(''); setNovoValor(''); setNovoVenc('')
      setMsg('Conta adicionada!')
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : String(e))
    } finally {
      setSaving(false)
    }
  }

  if (loading) return <p style={{ color: 'var(--tx3)' }}>Carregando...</p>

  const renderConta = (view: ContaView) => (
    <div key={`${view.registroData}-${view.tipo}-${view.index}`} className={`conta-item ${view.conta.pago ? 'pago' : ''}`}>
      <input type="checkbox" className="conta-check" checked={view.conta.pago} onChange={() => togglePago(view)} />
      <div className="conta-info">
        <div className="conta-desc">{view.conta.descricao}</div>
        <div className="conta-meta">
          Lançado em {fmtDate(view.registroData)}
          {view.conta.dataVencimento ? ` · Vence: ${fmtDate(view.conta.dataVencimento)}` : ''}
        </div>
      </div>
      <div className={`conta-valor ${view.tipo}`}>{fmtBRL(view.conta.valor)}</div>
    </div>
  )

  return (
    <>
      <div className="add-conta-form">
        <h4>＋ Nova Conta</h4>
        <div className="conta-form-row">
          <select value={novoTipo} onChange={e => setNovoTipo(e.target.value as 'receber' | 'pagar')}>
            <option value="receber">A Receber</option>
            <option value="pagar">A Pagar</option>
          </select>
          <input placeholder="Descrição" value={novaDesc} onChange={e => setNovaDesc(e.target.value)} />
        </div>
        <div className="conta-form-row">
          <input type="number" placeholder="Valor R$" value={novoValor} onChange={e => setNovoValor(e.target.value)} step="0.01" min="0" />
          <input type="date" value={novoVenc} onChange={e => setNovoVenc(e.target.value)} />
        </div>
        <button className="btn-add-conta" onClick={adicionarConta} disabled={saving || !novaDesc || !novoValor}>
          {saving ? 'Salvando...' : '＋ Adicionar'}
        </button>
        {msg && <div style={{ marginTop: 8, fontSize: 13, color: '#34c759' }}>{msg}</div>}
      </div>

      <div className="contas-section">
        <h3>📥 A Receber ({pendentesReceber.length})</h3>
        {pendentesReceber.length === 0 && <p style={{ color: 'var(--tx3)', fontSize: 13 }}>Nenhuma pendente.</p>}
        {pendentesReceber.map(renderConta)}
      </div>

      <div className="contas-section">
        <h3>📤 A Pagar ({pendentesPagar.length})</h3>
        {pendentesPagar.length === 0 && <p style={{ color: 'var(--tx3)', fontSize: 13 }}>Nenhuma pendente.</p>}
        {pendentesPagar.map(renderConta)}
      </div>

      {recebidasList.length > 0 && (
        <div className="contas-section">
          <h3>✅ Já Recebidas</h3>
          {recebidasList.map(renderConta)}
        </div>
      )}

      {pagasList.length > 0 && (
        <div className="contas-section">
          <h3>✅ Já Pagas</h3>
          {pagasList.map(renderConta)}
        </div>
      )}

      <div className="contas-section">
        <h3>🔁 Contas Recorrentes ({recorrentes.length})</h3>
        <div className="conta-form-row" style={{ marginBottom: 8 }}>
          <select value={novaRecTipo} onChange={e => setNovaRecTipo(e.target.value as 'Receber' | 'Pagar')}>
            <option value="Pagar">A Pagar</option>
            <option value="Receber">A Receber</option>
          </select>
          <input placeholder="Descrição" value={novaRecDesc} onChange={e => setNovaRecDesc(e.target.value)} />
          <input type="number" placeholder="Valor R$" value={novaRecValor} onChange={e => setNovaRecValor(e.target.value)} step="0.01" min="0" />
        </div>
        <div className="conta-form-row" style={{ marginBottom: 8 }}>
          <label style={{ fontSize: 12, color: 'var(--tx3)' }}>Início:</label>
          <input type="date" value={novaRecInicio} onChange={e => setNovaRecInicio(e.target.value)} />
          <label style={{ fontSize: 12, color: 'var(--tx3)' }}>Fim (opcional):</label>
          <input type="date" value={novaRecFim} onChange={e => setNovaRecFim(e.target.value)} />
        </div>
        <div className="conta-form-row" style={{ marginBottom: 8 }}>
          <label style={{ fontSize: 12, color: 'var(--tx3)' }}>Periodicidade:</label>
          <select value={novaRecPeriodicidade} onChange={e => setNovaRecPeriodicidade(e.target.value)}>
            <option value="Mensal">Mensal</option>
            <option value="Semanal">Semanal</option>
            <option value="Quinzenal">Quinzenal</option>
            <option value="Trimestral">Trimestral</option>
            <option value="Semestral">Semestral</option>
            <option value="Anual">Anual</option>
          </select>
          <input type="number" min="1" placeholder="Parcelas (opcional)" value={novaRecParcelas} onChange={e => setNovaRecParcelas(e.target.value)} />
        </div>
        <button className="btn-add-conta" onClick={adicionarRecorrente} disabled={savingRec || !novaRecDesc || !novaRecValor || !novaRecInicio}>
          {savingRec ? 'Salvando...' : '＋ Adicionar Recorrente'}
        </button>
        {recorrentes.map(r => (
          <div key={r.id} className="conta-item">
            <div className="conta-info">
              <div className="conta-desc">{r.descricao}</div>
              <div className="conta-meta">{r.tipo} · {fmtBRL(r.valor)} · {r.periodicidade} · Desde {fmtDate(r.dataInicio)}{r.dataFim ? ` até ${fmtDate(r.dataFim)}` : ''}{r.quantidadeParcelas ? ` · ${r.quantidadeParcelas}x` : ''}</div>
            </div>
            <button style={{ fontSize: 12, padding: '4px 10px', background: '#ff3b30', border: 'none', borderRadius: 6, color: '#fff', cursor: 'pointer' }} onClick={() => handleDesativar(r.id)}>Desativar</button>
          </div>
        ))}
      </div>
    </>
  )
}
