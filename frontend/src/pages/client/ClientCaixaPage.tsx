import { useState, useEffect, useCallback } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { useRegistros } from '../../hooks/useRegistros'
import StatCard from '../../components/shared/StatCard'
import DayNav from '../../components/shared/DayNav'
import { fmtBRL, todayISO, addDays } from '../../utils/format'
import type { Saida, ContaProvisionada } from '../../types'
import './ClientCaixa.css'

interface Props { clienteIdOverride?: string }

export default function ClientCaixaPage({ clienteIdOverride }: Props) {
  const { user } = useAuth()
  const clienteId = clienteIdOverride ?? user?.usuarioId ?? null
  const { registros, salvar, buscarPorData } = useRegistros(clienteId)

  const [data, setData] = useState(todayISO())
  const [inicio, setInicio] = useState(0)
  const [entrada, setEntrada] = useState('')
  const [saidas, setSaidas] = useState<Saida[]>([{ descricao: '', valor: 0 }])
  const [aReceber, setAReceber] = useState<ContaProvisionada[]>([])
  const [aPagar, setAPagar] = useState<ContaProvisionada[]>([])
  const [confirmado, setConfirmado] = useState('')
  const [saving, setSaving] = useState(false)
  const [msg, setMsg] = useState('')

  const totalSaidas = saidas.reduce((s, x) => s + (Number(x.valor) || 0), 0)
  const calculado = inicio + (Number(entrada) || 0) - totalSaidas
  const dif = confirmado !== '' ? calculado - Number(confirmado) : null

  const carregarDia = useCallback(async (d: string) => {
    const reg = await buscarPorData(d)
    if (reg) {
      setInicio(reg.saldoInicio)
      setEntrada(String(reg.entrada))
      setSaidas(reg.saidas.length ? reg.saidas : [{ descricao: '', valor: 0 }])
      setAReceber(reg.contasAReceber)
      setAPagar(reg.contasAPagar)
      setConfirmado(String(reg.saldoConfirmado))
    } else {
      const prev = registros.find(r => r.data < d)
      setInicio(prev?.saldoConfirmado ?? 0)
      setEntrada('')
      setSaidas([{ descricao: '', valor: 0 }])
      setAReceber([])
      setAPagar([])
      setConfirmado('')
    }
  }, [buscarPorData, registros])

  useEffect(() => { carregarDia(data) }, [data])

  async function handleSave() {
    if (!clienteId) return
    setSaving(true)
    setMsg('')
    try {
      await salvar({
        clienteId,
        data,
        saldoInicio: inicio,
        entrada: Number(entrada) || 0,
        saidas: saidas.filter(s => s.descricao || s.valor),
        contasAReceber: aReceber.filter(s => s.descricao || s.valor),
        contasAPagar: aPagar.filter(s => s.descricao || s.valor),
        saldoConfirmado: Number(confirmado) || calculado,
      })
      setMsg('✅ Salvo com sucesso!')
    } catch (e: any) {
      setMsg(`❌ ${e.message}`)
    } finally {
      setSaving(false)
    }
  }

  function updateSaida(i: number, field: keyof Saida, val: string) {
    setSaidas(s => s.map((x, j) => j === i ? { ...x, [field]: field === 'valor' ? Number(val) : val } : x))
  }
  function updateProv(list: ContaProvisionada[], setList: (v: ContaProvisionada[]) => void, i: number, field: keyof ContaProvisionada, val: string) {
    setList(list.map((x, j) => j === i ? { ...x, [field]: field === 'valor' ? Number(val) : val } : x))
  }

  return (
    <>
      <DayNav date={data} onPrev={() => setData(d => addDays(d, -1))} onNext={() => setData(d => addDays(d, 1))} />
      <div className="stats-grid">
        <StatCard label="📥 Início" value={fmtBRL(inicio)} />
        <StatCard label="📤 Entrada" value={fmtBRL(Number(entrada) || 0)} className="val-green" />
        <StatCard label="💸 Saídas" value={fmtBRL(totalSaidas)} className="val-red" />
        <StatCard label="💰 Saldo" value={fmtBRL(calculado)} className="val-green" />
      </div>

      <div className="form-card">
        <h3>📋 Registro do dia</h3>
        <div className="inp-group">
          <label>Saldo início (preenchido automaticamente)</label>
          <input type="number" value={inicio} readOnly style={{ color: 'var(--tx4)', cursor: 'not-allowed' }} />
        </div>
        <div className="inp-group">
          <label>💵 Dinheiro que entrou hoje (R$)</label>
          <input type="number" value={entrada} onChange={e => setEntrada(e.target.value)} placeholder="0,00" step="0.01" min="0" />
        </div>
        <div className="inp-group">
          <label>💸 Saídas do dia</label>
          {saidas.map((s, i) => (
            <div key={i} className="saida-row">
              <input placeholder="Descrição" value={s.descricao} onChange={e => updateSaida(i, 'descricao', e.target.value)} />
              <input type="number" placeholder="R$" value={s.valor || ''} onChange={e => updateSaida(i, 'valor', e.target.value)} step="0.01" min="0" />
              <button className="btn-rm" onClick={() => setSaidas(s => s.filter((_, j) => j !== i))}>✕</button>
            </div>
          ))}
          <button className="btn-add-saida" onClick={() => setSaidas(s => [...s, { descricao: '', valor: 0 }])}>＋ Adicionar saída</button>
        </div>
      </div>

      <div className="form-card">
        <h3>📥 Contas a Receber (Provisionamento)</h3>
        {aReceber.map((s, i) => (
          <div key={i} className="prov-row">
            <input placeholder="Descrição" value={s.descricao} onChange={e => updateProv(aReceber, setAReceber, i, 'descricao', e.target.value)} />
            <input type="number" placeholder="R$" value={s.valor || ''} onChange={e => updateProv(aReceber, setAReceber, i, 'valor', e.target.value)} step="0.01" min="0" />
            <button className="btn-rm-prov" onClick={() => setAReceber(a => a.filter((_, j) => j !== i))}>✕</button>
          </div>
        ))}
        <button className="btn-add-receber" onClick={() => setAReceber(a => [...a, { descricao: '', valor: 0 }])}>＋ Adicionar a Receber</button>
      </div>

      <div className="form-card">
        <h3>📤 Contas a Pagar (Provisionamento)</h3>
        {aPagar.map((s, i) => (
          <div key={i} className="prov-row">
            <input placeholder="Descrição" value={s.descricao} onChange={e => updateProv(aPagar, setAPagar, i, 'descricao', e.target.value)} />
            <input type="number" placeholder="R$" value={s.valor || ''} onChange={e => updateProv(aPagar, setAPagar, i, 'valor', e.target.value)} step="0.01" min="0" />
            <button className="btn-rm-prov" onClick={() => setAPagar(a => a.filter((_, j) => j !== i))}>✕</button>
          </div>
        ))}
        <button className="btn-add-pagar" onClick={() => setAPagar(a => [...a, { descricao: '', valor: 0 }])}>＋ Adicionar a Pagar</button>
      </div>

      <div className="saldo-box">
        <div>
          <div className="saldo-calc-lbl">Saldo calculado</div>
          <div className="saldo-calc-val">{fmtBRL(calculado)}</div>
        </div>
        <div style={{ textAlign: 'right' }}>
          <div className="saldo-calc-lbl">Confirmar saldo (R$)</div>
          <input type="number" value={confirmado} onChange={e => setConfirmado(e.target.value)} placeholder="0,00" step="0.01"
            style={{ width: 140, padding: '8px 12px', background: '#111', border: '2px solid #34c759', borderRadius: 8, color: '#fff', fontSize: 17, fontWeight: 700, textAlign: 'right' }} />
        </div>
      </div>
      {dif !== null && (
        <div className={`dif-msg ${Math.abs(dif) < 0.01 ? 'val-green' : 'val-red'}`}>
          {Math.abs(dif) < 0.01 ? '✅ Saldo conferido!' : `⚠️ Diferença: ${fmtBRL(Math.abs(dif))}`}
        </div>
      )}
      {msg && <div style={{ marginTop: 8, fontWeight: 600, color: msg.startsWith('✅') ? '#34c759' : '#ff6b6b' }}>{msg}</div>}
      <button className="btn-save" onClick={handleSave} disabled={saving}>
        {saving ? 'Salvando...' : '☁️ Salvar e sincronizar'}
      </button>
    </>
  )
}
