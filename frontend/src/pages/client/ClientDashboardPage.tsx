import { useState, useEffect, useMemo } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'
import { useRegistros } from '../../hooks/useRegistros'
import { useMetricas } from '../../hooks/useMetricas'
import StatCard from '../../components/shared/StatCard'
import { fmtBRL, todayISO, addDays } from '../../utils/format'
import { obterMeta, salvarMeta } from '../../api/metas'
import { getContasEmRisco, agruparVencimentos } from '../../utils/alertas'
import type { MetaAnual } from '../../types'
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, PieChart, Pie, Cell, Legend } from 'recharts'
import { grupoDaCategoria, CORES_GRUPO } from '../../utils/categorias'
import './ClientDashboard.css'

interface Props { clienteIdOverride?: string }

const MONTHS = ['Jan','Fev','Mar','Abr','Mai','Jun','Jul','Ago','Set','Out','Nov','Dez']

export default function ClientDashboardPage({ clienteIdOverride }: Props) {
  const { user } = useAuth()
  const clienteId = clienteIdOverride ?? user?.usuarioId ?? null
  const { registros, loading } = useRegistros(clienteId)
  const navigate = useNavigate()

  const contasEmRisco = useMemo(() => getContasEmRisco(registros), [registros])
  const { vencemHoje, proximos7Dias } = useMemo(() => agruparVencimentos(registros), [registros])

  const saldoProjetado = useMemo(() => {
    const saldoAtual = registros[0]?.saldoConfirmado ?? 0
    const totalReceber = registros.flatMap(r => r.contasAReceber).filter(c => !c.pago).reduce((s, c) => s + c.valor, 0)
    const totalPagar = registros.flatMap(r => r.contasAPagar).filter(c => !c.pago).reduce((s, c) => s + c.valor, 0)
    return saldoAtual + totalReceber - totalPagar
  }, [registros])

  const hoje = new Date()
  const anoAtual = hoje.getFullYear()
  const mesAtual = hoje.getMonth() + 1

  const lucroComparativo = useMemo(() => {
    const lucroDoMes = (ano: number, mes: number) => {
      const prefixo = `${ano}-${String(mes).padStart(2, '0')}`
      const doMes = registros.filter(r => r.data.startsWith(prefixo))
      return doMes.reduce((s, r) =>
        s + r.entradas.reduce((a, e) => a + e.valor, 0) - r.saidas.reduce((a, e) => a + e.valor, 0), 0)
    }
    const atual = lucroDoMes(anoAtual, mesAtual)
    const mesAnt = mesAtual === 1 ? 12 : mesAtual - 1
    const anoAnt = mesAtual === 1 ? anoAtual - 1 : anoAtual
    const anterior = lucroDoMes(anoAnt, mesAnt)
    const variacao = anterior !== 0 ? ((atual - anterior) / Math.abs(anterior)) * 100 : null
    return { atual, anterior, variacao }
  }, [registros, anoAtual, mesAtual])

  const primeiroDiaMes = `${anoAtual}-${String(mesAtual).padStart(2,'0')}-01`
  const ultimoDiaMes = new Date(anoAtual, mesAtual, 0).toISOString().slice(0,10)

  const [de, setDe] = useState(primeiroDiaMes)
  const [ate, setAte] = useState(ultimoDiaMes)
  const [multiploValuation, setMultiploValuation] = useState(3)
  const { metricas, fluxo } = useMetricas(clienteId, de, ate, multiploValuation)
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

  const composicaoDespesas = useMemo(() => {
    const acc: Record<string, number> = {}
    for (const r of doPeriodo)
      for (const s of r.saidas) {
        const grupo = grupoDaCategoria(s.categoria)
        acc[grupo] = (acc[grupo] ?? 0) + s.valor
      }
    return Object.entries(acc).map(([name, value]) => ({ name, value })).filter(d => d.value > 0)
  }, [doPeriodo])

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

  const metaMesAtual = useMemo(() => {
    const linha = planejamento.find(p => p.isAtual)
    if (!linha || linha.lucroReal === null) return null
    const alvo = linha.targetLucro
    const real = linha.lucroReal
    const pct = alvo > 0 ? Math.min(100, Math.max(0, (real / alvo) * 100)) : 0
    const faltante = Math.max(0, alvo - real)
    return { alvo, real, pct, faltante, atingida: real >= alvo }
  }, [planejamento])

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
        <button type="button" onClick={() => { const h = todayISO(); setDe(h); setAte(h) }}
          style={{ fontSize: 12, padding: '4px 10px', borderRadius: 6, border: '1px solid var(--bd)', background: 'var(--bg-card)', color: 'var(--tx1)', cursor: 'pointer' }}>
          Hoje
        </button>
        <button type="button" onClick={() => { setDe(addDays(todayISO(), -29)); setAte(todayISO()) }}
          style={{ fontSize: 12, padding: '4px 10px', borderRadius: 6, border: '1px solid var(--bd)', background: 'var(--bg-card)', color: 'var(--tx1)', cursor: 'pointer' }}>
          Últimos 30 dias
        </button>
      </div>

      <div className="stats-grid">
        <StatCard label="📈 Total Receita" value={fmtBRL(totalReceita)} className="val-green" />
        <StatCard label="💸 Total Saída" value={fmtBRL(totalSaida)} className="val-red" />
        <StatCard label="📊 Lucro Operacional" value={fmtBRL(lucroOp)} className={lucroOp >= 0 ? 'val-green' : 'val-red'} />
        <StatCard
          label="🧮 Lucro Líquido (mês)"
          value={fmtBRL(lucroComparativo.atual)}
          className={lucroComparativo.atual >= 0 ? 'val-green' : 'val-red'}
          sub={lucroComparativo.variacao === null
            ? `Mês anterior: ${fmtBRL(lucroComparativo.anterior)}`
            : `${lucroComparativo.variacao >= 0 ? '▲' : '▼'} ${Math.abs(lucroComparativo.variacao).toFixed(1)}% vs mês anterior`}
        />
        <StatCard label="💰 Saldo Final" value={fmtBRL(saldoFinal)} className="val-blue" />
      </div>

      {contasEmRisco.length > 0 && (
        <div className="meta-card" style={{ borderColor: '#ff9500' }}>
          <h3>⚠️ Alertas de Vencimento ({contasEmRisco.length})</h3>
          {contasEmRisco.slice(0, 5).map((c, i) => (
            <div key={i} style={{ display: 'flex', justifyContent: 'space-between', padding: '6px 0', borderBottom: '1px solid var(--bd)', fontSize: 13 }}>
              <span style={{ color: c.vencida ? '#ff3b30' : '#ff9500' }}>
                {c.vencida ? '🔴' : '🟡'} {c.conta.descricao}
              </span>
              <span>{fmtBRL(c.conta.valor)} · {c.conta.dataVencimento}</span>
            </div>
          ))}
          <button onClick={() => navigate('contas')} style={{ marginTop: 10, fontSize: 12, background: 'none', border: '1px solid var(--bd)', borderRadius: 6, color: 'var(--tx3)', padding: '4px 12px', cursor: 'pointer' }}>
            Ver todas as contas →
          </button>
        </div>
      )}

      {vencemHoje.length > 0 && (
        <div className="meta-card" style={{ borderColor: '#ff3b30' }}>
          <h3>🔴 Vencem Hoje ({vencemHoje.length})</h3>
          {vencemHoje.map((c, i) => (
            <div key={i} style={{ display: 'flex', justifyContent: 'space-between', padding: '6px 0', borderBottom: '1px solid var(--bd)', fontSize: 13 }}>
              <span>{c.tipo === 'receber' ? '📥' : '📤'} {c.conta.descricao}</span>
              <span>{fmtBRL(c.conta.valor)}</span>
            </div>
          ))}
        </div>
      )}

      {proximos7Dias.length > 0 && (
        <div className="meta-card" style={{ borderColor: '#ff9500' }}>
          <h3>🗓️ Próximos 7 dias ({proximos7Dias.length})</h3>
          {proximos7Dias.map((c, i) => (
            <div key={i} style={{ display: 'flex', justifyContent: 'space-between', padding: '6px 0', borderBottom: '1px solid var(--bd)', fontSize: 13 }}>
              <span>{c.tipo === 'receber' ? '📥' : '📤'} {c.conta.descricao}</span>
              <span>{fmtBRL(c.conta.valor)} · {c.conta.dataVencimento}</span>
            </div>
          ))}
        </div>
      )}

      <div className="meta-card">
        <h3>💵 Saldo Projetado</h3>
        <p style={{ fontSize: 12, color: 'var(--tx3)', marginBottom: 8 }}>Saldo atual + contas a receber pendentes − contas a pagar pendentes</p>
        <div style={{ fontSize: 22, fontWeight: 700, color: saldoProjetado >= 0 ? '#34c759' : '#ff3b30' }}>
          {fmtBRL(saldoProjetado)}
        </div>
      </div>

      {metricas && (
        <div className="stats-grid" style={{ marginTop: 16 }}>
          {metricas.ebitda && (
            <StatCard
              label={`📊 EBITDA ${metricas.ebitda.semaforo === 'verde' ? '🟢' : metricas.ebitda.semaforo === 'amarelo' ? '🟡' : '🔴'}`}
              value={`${fmtBRL(metricas.ebitda.valor)} (${((metricas.ebitda.percentual ?? 0) * 100).toFixed(1)}%)`}
              className={metricas.ebitda.semaforo === 'verde' ? 'val-green' : metricas.ebitda.semaforo === 'amarelo' ? 'val-yellow' : 'val-red'}
            />
          )}
          {metricas.ticketMedio && (
            <StatCard
              label="🎟️ Ticket Médio"
              value={fmtBRL(metricas.ticketMedio.valor)}
              className="val-blue"
              sub={`${metricas.ticketMedio.quantidadeRecebimentos} recebimentos`}
            />
          )}
          {metricas.primeCost?.percentual != null && (
            <StatCard
              label={`🍽️ Prime Cost ${metricas.primeCost.semaforo === 'verde' ? '🟢' : metricas.primeCost.semaforo === 'amarelo' ? '🟡' : '🔴'}`}
              value={`${((metricas.primeCost.percentual) * 100).toFixed(1)}%`}
              className={metricas.primeCost.semaforo === 'verde' ? 'val-green' : metricas.primeCost.semaforo === 'amarelo' ? 'val-yellow' : 'val-red'}
            />
          )}
          {metricas.pontoDeEquilibrio && (
            <StatCard
              label={`⚖️ Ponto de Equilíbrio ${metricas.pontoDeEquilibrio.semaforo === 'verde' ? '🟢' : metricas.pontoDeEquilibrio.semaforo === 'amarelo' ? '🟡' : '🔴'}`}
              value={fmtBRL(metricas.pontoDeEquilibrio.valor)}
              className={metricas.pontoDeEquilibrio.semaforo === 'verde' ? 'val-green' : metricas.pontoDeEquilibrio.semaforo === 'amarelo' ? 'val-yellow' : 'val-red'}
            />
          )}
          {metricas.valuation && (
            <>
              <div style={{ display: 'flex', gap: 6, alignItems: 'center', margin: '8px 0' }}>
                <span style={{ fontSize: 12, color: 'var(--tx3)' }}>Múltiplo Valuation:</span>
                {[3, 4, 5, 6].map(m => (
                  <button key={m} type="button" onClick={() => setMultiploValuation(m)}
                    style={{ padding: '4px 10px', borderRadius: 6, border: '1px solid var(--bd)', cursor: 'pointer',
                      background: multiploValuation === m ? '#0a84ff' : 'var(--bg-card)', color: multiploValuation === m ? '#fff' : 'var(--tx1)' }}>
                    {m}x
                  </button>
                ))}
              </div>
              <StatCard
                label={`💎 Valuation ${metricas.valuation.semaforo === 'verde' ? '🟢' : metricas.valuation.semaforo === 'amarelo' ? '🟡' : '🔴'}`}
                value={fmtBRL(metricas.valuation.valor)}
                className={metricas.valuation.semaforo === 'verde' ? 'val-green' : 'val-blue'}
              />
            </>
          )}
          {metricas.runway && (
            <StatCard
              label={`⏳ Runway ${metricas.runway.semaforo === 'verde' ? '🟢' : metricas.runway.semaforo === 'amarelo' ? '🟡' : '🔴'}`}
              value={`${metricas.runway.meses.toFixed(1)} meses`}
              className={metricas.runway.semaforo === 'verde' ? 'val-green' : metricas.runway.semaforo === 'amarelo' ? 'val-yellow' : 'val-red'}
            />
          )}
          {metricas.liquidez && (
            <StatCard
              label={`💧 Liquidez ${metricas.liquidez.semaforo === 'verde' ? '🟢' : metricas.liquidez.semaforo === 'amarelo' ? '🟡' : '🔴'}`}
              value={metricas.liquidez.altaLiquidez ? 'Alta liquidez' : `${metricas.liquidez.indice?.toFixed(2)}×`}
              className={metricas.liquidez.semaforo === 'verde' ? 'val-green' : metricas.liquidez.semaforo === 'amarelo' ? 'val-yellow' : 'val-red'}
            />
          )}
          {metricas.burnRate != null && (
            <StatCard label="🔥 Burn Rate (mês)" value={fmtBRL(metricas.burnRate)} className="val-red" />
          )}
        </div>
      )}

      {fluxo && fluxo.dias.length > 0 && (
        <div className="meta-card">
          <h3>📈 Fluxo de Caixa Projetado (30 dias)</h3>
          <div style={{ height: 200 }}>
            <ResponsiveContainer width="100%" height="100%">
              <LineChart data={fluxo.dias.map(d => ({ dia: d.data.slice(5), saldo: d.saldoProjetado }))}>
                <CartesianGrid strokeDasharray="3 3" stroke="var(--bd)" />
                <XAxis dataKey="dia" stroke="var(--tx3)" tick={{ fontSize: 11 }} interval={4} />
                <YAxis stroke="var(--tx3)" tick={{ fontSize: 11 }} tickFormatter={v => `R$${(v/1000).toFixed(0)}k`} />
                <Tooltip formatter={(v) => typeof v === 'number' ? fmtBRL(v) : String(v)} contentStyle={{ background: 'var(--bg-card)', border: '1px solid var(--bd)' }} />
                <Line type="monotone" dataKey="saldo" stroke="#0a84ff" strokeWidth={2} dot={false} />
              </LineChart>
            </ResponsiveContainer>
          </div>
        </div>
      )}

      {composicaoDespesas.length > 0 && (
        <div className="meta-card">
          <h3>🥧 Composição das Despesas</h3>
          <div style={{ height: 260 }}>
            <ResponsiveContainer width="100%" height="100%">
              <PieChart>
                <Pie data={composicaoDespesas} dataKey="value" nameKey="name" cx="50%" cy="50%" outerRadius={90} label>
                  {composicaoDespesas.map((d) => <Cell key={d.name} fill={CORES_GRUPO[d.name] ?? '#888'} />)}
                </Pie>
                <Tooltip formatter={(v) => typeof v === 'number' ? fmtBRL(v) : String(v)} />
                <Legend />
              </PieChart>
            </ResponsiveContainer>
          </div>
        </div>
      )}

      {metaMesAtual && (
        <div className="meta-card">
          <h3>🎯 Meta de Lucro do Mês</h3>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13, color: 'var(--tx3)', marginBottom: 6 }}>
            <span>Realizado: {fmtBRL(metaMesAtual.real)}</span>
            <span>Meta: {fmtBRL(metaMesAtual.alvo)}</span>
          </div>
          <div style={{ height: 14, background: 'var(--bd)', borderRadius: 8, overflow: 'hidden' }}>
            <div style={{
              width: `${metaMesAtual.pct}%`, height: '100%',
              background: metaMesAtual.atingida ? '#34c759' : '#0a84ff', transition: 'width .3s',
            }} />
          </div>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 8, fontSize: 14, fontWeight: 600 }}>
            <span style={{ color: metaMesAtual.atingida ? '#34c759' : 'var(--tx1)' }}>
              {metaMesAtual.pct.toFixed(1)}% atingido
            </span>
            <span style={{ color: metaMesAtual.atingida ? '#34c759' : '#ff9500' }}>
              {metaMesAtual.atingida ? '✅ Meta batida!' : `Faltam ${fmtBRL(metaMesAtual.faltante)}`}
            </span>
          </div>
        </div>
      )}

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
