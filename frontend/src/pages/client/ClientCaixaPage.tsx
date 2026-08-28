import { useState, useEffect, useRef } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { useRegistros } from '../../hooks/useRegistros'
import StatCard from '../../components/shared/StatCard'
import DayNav from '../../components/shared/DayNav'
import { fmtBRL, todayISO, addDays } from '../../utils/format'
import { listarCategorias } from '../../api/categorias'
import { listarContasBancarias } from '../../api/contasBancarias'
import { ORDEM_GRUPOS } from '../../utils/categorias'
import type { ItemFinanceiro, ItemFinanceiroSaida, Categorias, ContaBancaria } from '../../types'
import './ClientCaixa.css'

interface Props { clienteIdOverride?: string }

// Cada linha carrega sua própria conta de destino — default é a última usada na sessão (ou o
// Caixa). Quem só movimenta dinheiro numa conta só nunca vê esse campo (seletor só aparece com 2+
// contas ativas), então não precisa tocar em nada.
type LinhaEntrada = ItemFinanceiro & { contaId: string }
type LinhaSaida = ItemFinanceiroSaida & { contaId: string }

// Id gerado no cliente pra todo lançamento novo nascer com identidade própria — sem isso, baixas
// de Contas a Pagar/Receber não conseguem vincular com segurança a um lançamento manual específico.
const novaEntrada = (contaId = ''): LinhaEntrada => ({ id: crypto.randomUUID(), descricao: '', valor: 0, contaId })
const novaSaida = (contaId = ''): LinhaSaida => ({ id: crypto.randomUUID(), descricao: '', valor: 0, categoria: '', subcategoria: '', contaId })

function agruparPorConta<T extends { contaId: string }>(itens: T[]): Map<string, T[]> {
  const mapa = new Map<string, T[]>()
  for (const item of itens) {
    const lista = mapa.get(item.contaId)
    if (lista) lista.push(item)
    else mapa.set(item.contaId, [item])
  }
  return mapa
}

function fmtNum(n: number) {
  if (!n) return ''
  return n.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}
function parseBRL(s: string): number {
  return parseFloat(s.replace(/\./g, '').replace(',', '.')) || 0
}

export default function ClientCaixaPage({ clienteIdOverride }: Props) {
  const { user } = useAuth()
  const clienteId = clienteIdOverride ?? user?.usuarioId ?? null
  const { registros, salvar, buscarPorData } = useRegistros(clienteId)

  const [data, setData] = useState(todayISO())
  const [inicio, setInicio] = useState(0)
  const [entradas, setEntradas] = useState<LinhaEntrada[]>([novaEntrada()])
  const [saidas, setSaidas] = useState<LinhaSaida[]>([novaSaida()])
  const [entradaDisplays, setEntradaDisplays] = useState<string[]>([''])
  const [saidaDisplays, setSaidaDisplays] = useState<string[]>([''])
  const [confirmado, setConfirmado] = useState('')
  const [saving, setSaving] = useState(false)
  const [msg, setMsg] = useState('')
  const [categorias, setCategorias] = useState<Categorias>({ entradas: [], saidas: [] })
  const [saveSuccess, setSaveSuccess] = useState(false)
  const [catOpen, setCatOpen] = useState<{ tipo: 'entrada' | 'saida'; idx: number } | null>(null)
  const [savedEntradas, setSavedEntradas] = useState<LinhaEntrada[]>([])
  const [savedSaidas, setSavedSaidas] = useState<LinhaSaida[]>([])
  const dropdownRef = useRef<HTMLDivElement>(null)
  const [contas, setContas] = useState<ContaBancaria[]>([])
  const [contaId, setContaId] = useState<string>('')
  // Lembra a última conta usada na sessão — vira o default de cada linha nova.
  const [ultimaContaUsada, setUltimaContaUsada] = useState<string>('')

  useEffect(() => {
    listarCategorias().then(setCategorias).catch(console.error)
  }, [])

  useEffect(() => {
    if (!clienteId) return
    listarContasBancarias(clienteId)
      .then(cs => {
        setContas(cs)
        const ativas = cs.filter(c => c.ativa)
        if (ativas.length > 0 && !contaId) {
          const caixa = ativas.find(c => c.tipo === 'Caixa') ?? ativas[0]
          setContaId(caixa.id)
          setUltimaContaUsada(caixa.id)
        }
      })
      .catch(console.error)
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [clienteId])

  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setCatOpen(null)
      }
    }
    if (catOpen) document.addEventListener('mousedown', handleClick)
    return () => document.removeEventListener('mousedown', handleClick)
  }, [catOpen])

  const totalEntradas = [...entradas, ...savedEntradas].reduce((s, x) => s + (Number(x.valor) || 0), 0)
  const totalSaidas = [...saidas, ...savedSaidas].reduce((s, x) => s + (Number(x.valor) || 0), 0)
  const calculado = inicio + totalEntradas - totalSaidas
  const dif = confirmado !== '' ? calculado - Number(confirmado) : null

  useEffect(() => {
    if (!clienteId || !contaId) return
    let ignore = false
    const load = async () => {
      const reg = await buscarPorData(data, contaId)
      if (ignore) return
      if (reg) {
        setInicio(reg.saldoInicio)
        setSavedEntradas(reg.entradas.map(e => ({ ...e, contaId })))
        setSavedSaidas(reg.saidas.map(s => ({ ...s, contaId })))
        setEntradas([novaEntrada(ultimaContaUsada || contaId)])
        setSaidas([novaSaida(ultimaContaUsada || contaId)])
        setEntradaDisplays([''])
        setSaidaDisplays([''])
        setConfirmado(String(reg.saldoConfirmado))
      } else {
        const prev = registros.filter(r => r.contaBancariaId === contaId).find(r => r.data < data)
        setInicio(prev?.saldoConfirmado ?? 0)
        setSavedEntradas([])
        setSavedSaidas([])
        setEntradas([novaEntrada(ultimaContaUsada || contaId)])
        setSaidas([novaSaida(ultimaContaUsada || contaId)])
        setEntradaDisplays([''])
        setSaidaDisplays([''])
        setConfirmado('')
      }
    }
    load()
    return () => { ignore = true }
  }, [data, clienteId, contaId, registros, buscarPorData])

  function handleSaveEntrada(idx: number) {
    const item = entradas[idx]
    if (!item.descricao && !item.valor) return
    setSavedEntradas(prev => [...prev, item])
    setEntradas(prev => {
      const next = prev.filter((_, j) => j !== idx)
      return next.length ? next : [novaEntrada(ultimaContaUsada)]
    })
    setEntradaDisplays(prev => {
      const next = prev.filter((_, j) => j !== idx)
      return next.length ? next : ['']
    })
  }

  function handleSaveSaida(idx: number) {
    const item = saidas[idx]
    if (!item.descricao && !item.valor) return
    if (!item.categoria) { setMsg('Selecione uma categoria para a saída.'); setSaveSuccess(false); return }
    setSavedSaidas(prev => [...prev, item])
    setSaidas(prev => {
      const next = prev.filter((_, j) => j !== idx)
      return next.length ? next : [novaSaida(ultimaContaUsada)]
    })
    setSaidaDisplays(prev => {
      const next = prev.filter((_, j) => j !== idx)
      return next.length ? next : ['']
    })
    setMsg('')
  }

  async function handleSave() {
    if (!clienteId) return
    setSaving(true)
    setMsg('')
    try {
      const todasEntradas = [...savedEntradas, ...entradas.filter(e => e.descricao || e.valor)]
      const todasSaidas = [...savedSaidas, ...saidas.filter(s => s.descricao || s.valor)]
      if (todasSaidas.some(s => !s.categoria)) {
        setSaveSuccess(false)
        setMsg('Selecione uma categoria para cada saída.')
        return
      }

      // Cada linha pode ter sua própria conta — agrupa e salva um registro por conta envolvida,
      // preservando os lançamentos e pendências que já existiam em cada uma.
      const gruposEntrada = agruparPorConta(todasEntradas)
      const gruposSaida = agruparPorConta(todasSaidas)
      const contasEnvolvidas = new Set([...gruposEntrada.keys(), ...gruposSaida.keys()])
      if (contasEnvolvidas.size === 0) contasEnvolvidas.add(contaId)

      for (const grupoContaId of contasEnvolvidas) {
        const entradasGrupo = gruposEntrada.get(grupoContaId) ?? []
        const saidasGrupo = gruposSaida.get(grupoContaId) ?? []
        const regExistente = await buscarPorData(data, grupoContaId)
        const inicioGrupo = regExistente
          ? regExistente.saldoInicio
          : registros.filter(r => r.contaBancariaId === grupoContaId).find(r => r.data < data)?.saldoConfirmado ?? 0
        const totalEntradasGrupo = entradasGrupo.reduce((s, x) => s + (Number(x.valor) || 0), 0)
        const totalSaidasGrupo = saidasGrupo.reduce((s, x) => s + (Number(x.valor) || 0), 0)
        const calculadoGrupo = inicioGrupo + totalEntradasGrupo - totalSaidasGrupo
        const saldoConfirmadoGrupo = grupoContaId === contaId && confirmado !== ''
          ? Number(confirmado)
          : regExistente?.saldoConfirmado ?? calculadoGrupo

        await salvar({
          clienteId, contaBancariaId: grupoContaId, data, saldoInicio: inicioGrupo,
          entradas: [...(regExistente?.entradas ?? []), ...entradasGrupo],
          saidas: [...(regExistente?.saidas ?? []), ...saidasGrupo],
          contasAReceber: regExistente?.contasAReceber ?? [],
          contasAPagar: regExistente?.contasAPagar ?? [],
          saldoConfirmado: saldoConfirmadoGrupo,
        })
      }

      setSaveSuccess(true)
      setMsg('Salvo com sucesso!')
    } catch (e: unknown) {
      setSaveSuccess(false)
      setMsg(e instanceof Error ? e.message : String(e))
    } finally {
      setSaving(false)
    }
  }

  function tipoCustoDe(nome: string) {
    const cat = [...categorias.entradas, ...categorias.saidas].find(c => c.nome === nome)
    return cat ? (cat.tipoCusto as 'Receita' | 'CustoFixo' | 'CustoVariavel') : undefined
  }

  function updateEntrada(i: number, field: keyof ItemFinanceiro, val: string) {
    setEntradas(prev => prev.map((x, j) => {
      if (j !== i) return x
      const updated: LinhaEntrada = { ...x, [field]: field === 'valor' ? Number(val) : val }
      if (field === 'categoria') { const tc = tipoCustoDe(val); if (tc) updated.tipoCusto = tc }
      return updated
    }))
  }

  function updateSaida(i: number, field: keyof ItemFinanceiroSaida, val: string) {
    setSaidas(prev => prev.map((x, j) => {
      if (j !== i) return x
      const updated: LinhaSaida = { ...x, [field]: field === 'valor' ? Number(val) : val }
      if (field === 'categoria') { const tc = tipoCustoDe(val); if (tc) updated.tipoCusto = tc }
      return updated
    }))
  }

  function updateEntradaConta(i: number, novaContaId: string) {
    setEntradas(prev => prev.map((x, j) => j === i ? { ...x, contaId: novaContaId } : x))
    setUltimaContaUsada(novaContaId)
  }

  function updateSaidaConta(i: number, novaContaId: string) {
    setSaidas(prev => prev.map((x, j) => j === i ? { ...x, contaId: novaContaId } : x))
    setUltimaContaUsada(novaContaId)
  }

  function handleSelectCat(nome: string) {
    if (!catOpen) return
    if (catOpen.tipo === 'entrada') updateEntrada(catOpen.idx, 'categoria', nome)
    else updateSaida(catOpen.idx, 'categoria', nome)
    setCatOpen(null)
  }

  function catItemsForSaida() {
    return ORDEM_GRUPOS
      .map(g => ({ grupo: g, itens: categorias.saidas.filter(c => (c.grupo ?? 'Outros') === g) }))
      .filter(g => g.itens.length > 0)
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

      {contas.length > 1 && (
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 8 }}>
          <span style={{ fontSize: 13, color: 'var(--tx3)' }}>Conta:</span>
          {contas.filter(c => c.ativa).map(c => (
            <button key={c.id} type="button" onClick={() => { setContaId(c.id); setUltimaContaUsada(c.id) }}
              style={{
                padding: '5px 14px', borderRadius: 8, fontSize: 13, cursor: 'pointer',
                border: '1px solid var(--bd)',
                background: contaId === c.id ? '#0a84ff' : 'var(--bg-card)',
                color: contaId === c.id ? '#fff' : 'var(--tx1)',
              }}>
              {c.nome}
            </button>
          ))}
        </div>
      )}

      <div className="form-card">
        <h3>📋 Registro do dia</h3>
        <div className="inp-group">
          <label>Saldo início (preenchido automaticamente)</label>
          <input type="text" value={fmtNum(inicio)} readOnly style={{ color: 'var(--tx4)', cursor: 'not-allowed' }} />
        </div>

        <div className="inp-group">
          <label>💵 Entradas do dia (dinheiro)</label>
          {savedEntradas.length > 0 && (
            <div className="saved-summary">
              {savedEntradas.map((e, i) => (
                <span key={i} className="saved-badge">✔ {e.descricao || 'Entrada'} · {fmtBRL(e.valor)}</span>
              ))}
            </div>
          )}
          {entradas.map((e, i) => (
            <div key={i}>
            <div className="lancamento-row has-rm">
              <input className="lancamento-desc" placeholder="Descrição" value={e.descricao}
                onChange={ev => updateEntrada(i, 'descricao', ev.target.value)} />
              <div className="val-input-wrap">
                <span className="val-prefix">R$</span>
                <input
                  type="text" inputMode="decimal" placeholder="0,00"
                  value={entradaDisplays[i] ?? ''}
                  onChange={ev => {
                    const raw = ev.target.value.replace(/[^\d,]/g, '')
                    setEntradaDisplays(prev => prev.map((v, j) => j === i ? raw : v))
                    updateEntrada(i, 'valor', String(parseBRL(raw)))
                  }}
                  onBlur={() => {
                    const num = entradas[i]?.valor ?? 0
                    setEntradaDisplays(prev => prev.map((v, j) => j === i ? fmtNum(num) : v))
                  }}
                />
              </div>
              <div style={{ position: 'relative' }}>
                <button className="cat-btn cat-btn-entrada"
                  onClick={() => setCatOpen(catOpen?.tipo === 'entrada' && catOpen.idx === i ? null : { tipo: 'entrada', idx: i })}>
                  {e.categoria || 'Categoria'}
                </button>
                {catOpen?.tipo === 'entrada' && catOpen.idx === i && (
                  <div className="cat-dropdown" ref={dropdownRef}>
                    <div className="cat-items" style={{ padding: 6 }}>
                      {categorias.entradas.map(c => (
                        <button key={c.nome} className="cat-item"
                          style={{ borderColor: '#007aff44' }}
                          onClick={() => handleSelectCat(c.nome)}>
                          {c.nome}
                        </button>
                      ))}
                    </div>
                  </div>
                )}
              </div>
              <button className="btn-item-save btn-item-save-entrada"
                disabled={!e.descricao && !e.valor} onClick={() => handleSaveEntrada(i)}>✔</button>
              <button className="btn-rm" onClick={() => {
                setEntradas(prev => prev.filter((_, j) => j !== i))
                setEntradaDisplays(prev => prev.filter((_, j) => j !== i))
              }}>✕</button>
            </div>
            {contas.filter(c => c.ativa).length > 1 && (
              <div style={{ display: 'flex', alignItems: 'center', gap: 6, margin: '-4px 0 8px 2px' }}>
                <span style={{ fontSize: 11, color: 'var(--tx3)' }}>Conta:</span>
                <select
                  value={e.contaId || ultimaContaUsada}
                  onChange={ev => updateEntradaConta(i, ev.target.value)}
                  style={{ fontSize: 12, padding: '2px 6px', borderRadius: 6, border: '1px solid var(--bd)', background: 'var(--bg-input)', color: 'var(--tx1)' }}
                >
                  {contas.filter(c => c.ativa).map(c => <option key={c.id} value={c.id}>{c.nome}</option>)}
                </select>
              </div>
            )}
            </div>
          ))}
          <button className="btn-add-entrada" onClick={() => {
            setEntradas(e => [...e, novaEntrada(ultimaContaUsada)])
            setEntradaDisplays(d => [...d, ''])
          }}>＋ Adicionar Entrada</button>
        </div>

        <div className="inp-group">
          <label>💸 Saídas do dia</label>
          {savedSaidas.length > 0 && (
            <div className="saved-summary">
              {savedSaidas.map((s, i) => (
                <span key={i} className="saved-badge"
                  style={{ background: 'rgba(255,59,48,.1)', borderColor: 'rgba(255,59,48,.3)', color: '#ff6b6b' }}>
                  ✔ {s.descricao || 'Saída'} · {fmtBRL(s.valor)}
                </span>
              ))}
            </div>
          )}
          {saidas.map((s, i) => (
            <div key={i}>
            <div className="lancamento-row has-rm">
              <input className="lancamento-desc" placeholder="Descrição" value={s.descricao}
                onChange={ev => updateSaida(i, 'descricao', ev.target.value)} />
              <div className="val-input-wrap">
                <span className="val-prefix">R$</span>
                <input
                  type="text" inputMode="decimal" placeholder="0,00"
                  value={saidaDisplays[i] ?? ''}
                  onChange={ev => {
                    const raw = ev.target.value.replace(/[^\d,]/g, '')
                    setSaidaDisplays(prev => prev.map((v, j) => j === i ? raw : v))
                    updateSaida(i, 'valor', String(parseBRL(raw)))
                  }}
                  onBlur={() => {
                    const num = saidas[i]?.valor ?? 0
                    setSaidaDisplays(prev => prev.map((v, j) => j === i ? fmtNum(num) : v))
                  }}
                />
              </div>
              <div style={{ position: 'relative' }}>
                <button className={`cat-btn ${s.categoria ? 'cat-btn-saida com-cat' : 'cat-btn-saida sem-cat'}`}
                  onClick={() => setCatOpen(catOpen?.tipo === 'saida' && catOpen.idx === i ? null : { tipo: 'saida', idx: i })}>
                  {s.categoria || 'Categoria'}
                </button>
                {catOpen?.tipo === 'saida' && catOpen.idx === i && (
                  <div className="cat-dropdown" ref={dropdownRef}>
                    {catItemsForSaida().map(({ grupo, itens }) => (
                      <div key={grupo} className="cat-group" style={{ padding: '6px 8px 0' }}>
                        <div className="cat-group-label" style={{ borderColor: '#ff6b6b' }}>{grupo}</div>
                        <div className="cat-items">
                          {itens.map(c => (
                            <button key={c.nome} className="cat-item"
                              style={{ borderColor: '#ff6b6b44' }}
                              onClick={() => handleSelectCat(c.nome)}>
                              {c.nome}
                            </button>
                          ))}
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
              <button className="btn-item-save btn-item-save-saida"
                disabled={!s.descricao && !s.valor} onClick={() => handleSaveSaida(i)}>✔</button>
              <button className="btn-rm" onClick={() => {
                setSaidas(prev => prev.filter((_, j) => j !== i))
                setSaidaDisplays(prev => prev.filter((_, j) => j !== i))
              }}>✕</button>
            </div>
            {contas.filter(c => c.ativa).length > 1 && (
              <div style={{ display: 'flex', alignItems: 'center', gap: 6, margin: '-4px 0 8px 2px' }}>
                <span style={{ fontSize: 11, color: 'var(--tx3)' }}>Conta:</span>
                <select
                  value={s.contaId || ultimaContaUsada}
                  onChange={ev => updateSaidaConta(i, ev.target.value)}
                  style={{ fontSize: 12, padding: '2px 6px', borderRadius: 6, border: '1px solid var(--bd)', background: 'var(--bg-input)', color: 'var(--tx1)' }}
                >
                  {contas.filter(c => c.ativa).map(c => <option key={c.id} value={c.id}>{c.nome}</option>)}
                </select>
              </div>
            )}
            </div>
          ))}
          <button className="btn-add-saida" onClick={() => {
            setSaidas(s => [...s, novaSaida(ultimaContaUsada)])
            setSaidaDisplays(d => [...d, ''])
          }}>＋ Adicionar saída</button>
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
