import { useState, useEffect } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { useRegistros } from '../../hooks/useRegistros'
import StatCard from '../../components/shared/StatCard'
import DayNav from '../../components/shared/DayNav'
import { fmtBRL, todayISO, addDays } from '../../utils/format'
import { listarCategorias } from '../../api/categorias'
import { ORDEM_GRUPOS } from '../../utils/categorias'
import type { ItemFinanceiro, ItemFinanceiroSaida, Categorias, CategoriaItem } from '../../types'
import './ClientCaixa.css'

interface Props { clienteIdOverride?: string }

const novaSaida = (): ItemFinanceiroSaida => ({ descricao: '', valor: 0, categoria: '', subcategoria: '' })

export default function ClientCaixaPage({ clienteIdOverride }: Props) {
  const { user } = useAuth()
  const clienteId = clienteIdOverride ?? user?.usuarioId ?? null
  const { registros, salvar, buscarPorData } = useRegistros(clienteId)

  const [data, setData] = useState(todayISO())
  const [inicio, setInicio] = useState(0)
  const [entradas, setEntradas] = useState<ItemFinanceiro[]>([{ descricao: '', valor: 0 }])
  const [saidas, setSaidas] = useState<ItemFinanceiroSaida[]>([novaSaida()])
  const [confirmado, setConfirmado] = useState('')
  const [saving, setSaving] = useState(false)
  const [msg, setMsg] = useState('')
  const [categorias, setCategorias] = useState<Categorias>({ entradas: [], saidas: [] })
  const [saveSuccess, setSaveSuccess] = useState(false)

  useEffect(() => {
    listarCategorias().then(setCategorias).catch(console.error)
  }, [])

  const totalEntradas = entradas.reduce((s, x) => s + (Number(x.valor) || 0), 0)
  const totalSaidas = saidas.reduce((s, x) => s + (Number(x.valor) || 0), 0)
  const calculado = inicio + totalEntradas - totalSaidas
  const dif = confirmado !== '' ? calculado - Number(confirmado) : null

  useEffect(() => {
    if (!clienteId) return
    let ignore = false
    const load = async () => {
      const reg = await buscarPorData(data)
      if (ignore) return
      if (reg) {
        setInicio(reg.saldoInicio)
        setEntradas(reg.entradas.length ? reg.entradas : [{ descricao: '', valor: 0 }])
        setSaidas(reg.saidas.length ? reg.saidas : [novaSaida()])
        setConfirmado(String(reg.saldoConfirmado))
      } else {
        const prev = registros.find(r => r.data < data)
        setInicio(prev?.saldoConfirmado ?? 0)
        setEntradas([{ descricao: '', valor: 0 }])
        setSaidas([novaSaida()])
        setConfirmado('')
      }
    }
    load()
    return () => { ignore = true }
  }, [data, clienteId, registros, buscarPorData])

  async function handleSave() {
    if (!clienteId) return
    setSaving(true)
    setMsg('')
    try {
      const saidasValidas = saidas.filter(s => s.descricao || s.valor)
      if (saidasValidas.some(s => !s.categoria)) {
        setSaveSuccess(false)
        setMsg('Selecione uma categoria para cada saída.')
        return
      }
      const regAtual = await buscarPorData(data)
      await salvar({
        clienteId,
        data,
        saldoInicio: inicio,
        entradas: entradas.filter(e => e.descricao || e.valor),
        saidas: saidasValidas,
        contasAReceber: regAtual?.contasAReceber ?? [],
        contasAPagar: regAtual?.contasAPagar ?? [],
        saldoConfirmado: confirmado === '' ? calculado : Number(confirmado),
      })
      setSaveSuccess(true)
      setMsg('Salvo com sucesso!')
    } catch (e: unknown) {
      setSaveSuccess(false)
      setMsg(e instanceof Error ? e.message : String(e))
    } finally {
      setSaving(false)
    }
  }

  function tipoCustoDe(nome: string): 'Receita' | 'CustoFixo' | 'CustoVariavel' | undefined {
    const cat = [...categorias.entradas, ...categorias.saidas].find(c => c.nome === nome)
    return cat ? (cat.tipoCusto as 'Receita' | 'CustoFixo' | 'CustoVariavel') : undefined
  }

  function updateEntrada(i: number, field: keyof ItemFinanceiro, val: string) {
    setEntradas(prev => prev.map((x, j) => {
      if (j !== i) return x
      const updated: ItemFinanceiro = { ...x, [field]: field === 'valor' ? Number(val) : val }
      if (field === 'categoria') {
        const tc = tipoCustoDe(val)
        if (tc) updated.tipoCusto = tc
      }
      return updated
    }))
  }

  function updateSaida(i: number, field: keyof ItemFinanceiroSaida, val: string) {
    setSaidas(prev => prev.map((x, j) => {
      if (j !== i) return x
      const updated: ItemFinanceiroSaida = { ...x, [field]: field === 'valor' ? Number(val) : val }
      if (field === 'categoria') {
        const tc = tipoCustoDe(val)
        if (tc) updated.tipoCusto = tc
      }
      return updated
    }))
  }

  return (
    <>
      <DayNav date={data} onPrev={() => setData(d => addDays(d, -1))} onNext={() => setData(d => addDays(d, 1))} />
      <div className="stats-grid">
        <StatCard label="📥 Início" value={fmtBRL(inicio)} />
        <StatCard label="📤 Entradas" value={fmtBRL(totalEntradas)} className="val-green" />
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
          <label>💵 Entradas do dia (dinheiro)</label>
          {entradas.map((e, i) => (
            <div key={i} className="saida-row">
              <input placeholder="Descrição" value={e.descricao} onChange={ev => updateEntrada(i, 'descricao', ev.target.value)} />
              <input type="number" placeholder="R$" value={e.valor || ''} onChange={ev => updateEntrada(i, 'valor', ev.target.value)} step="0.01" min="0" />
              <select value={e.categoria ?? ''} onChange={ev => updateEntrada(i, 'categoria', ev.target.value)}>
                <option value="">Categoria</option>
                {categorias.entradas.map(c => <option key={c.nome} value={c.nome}>{c.nome}</option>)}
              </select>
              <button className="btn-rm" onClick={() => setEntradas(prev => prev.filter((_, j) => j !== i))}>✕</button>
            </div>
          ))}
          <button className="btn-add-receber" onClick={() => setEntradas(e => [...e, { descricao: '', valor: 0 }])}>＋ Adicionar Entrada</button>
        </div>

        <div className="inp-group">
          <label>💸 Saídas do dia</label>
          {saidas.map((s, i) => (
            <div key={i} className="saida-row">
              <input placeholder="Descrição" value={s.descricao} onChange={e => updateSaida(i, 'descricao', e.target.value)} />
              <input type="number" placeholder="R$" value={s.valor || ''} onChange={e => updateSaida(i, 'valor', e.target.value)} step="0.01" min="0" />
              <select value={s.categoria ?? ''} onChange={ev => updateSaida(i, 'categoria', ev.target.value)}>
                <option value="">Categoria</option>
                {ORDEM_GRUPOS.map(grupo => {
                  const itens: CategoriaItem[] = categorias.saidas.filter(c => (c.grupo ?? 'Outros') === grupo)
                  if (itens.length === 0) return null
                  return (
                    <optgroup key={grupo} label={grupo}>
                      {itens.map(c => <option key={c.nome} value={c.nome}>{c.nome}</option>)}
                    </optgroup>
                  )
                })}
              </select>
              <button className="btn-rm" onClick={() => setSaidas(prev => prev.filter((_, j) => j !== i))}>✕</button>
            </div>
          ))}
          <button className="btn-add-saida" onClick={() => setSaidas(s => [...s, novaSaida()])}>＋ Adicionar saída</button>
        </div>
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
      {msg && <div style={{ marginTop: 8, fontWeight: 600, color: saveSuccess ? '#34c759' : '#ff6b6b' }}>{msg}</div>}
      <button className="btn-save" onClick={handleSave} disabled={saving}>
        {saving ? 'Salvando...' : '☁️ Salvar e sincronizar'}
      </button>
    </>
  )
}
