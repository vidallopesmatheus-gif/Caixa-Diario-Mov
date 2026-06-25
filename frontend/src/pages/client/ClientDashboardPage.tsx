import { useState, useEffect, useMemo } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'
import { useRegistros } from '../../hooks/useRegistros'
import { useMetricas } from '../../hooks/useMetricas'
import StatCard from '../../components/shared/StatCard'
import { fmtBRL, todayISO, addDays } from '../../utils/format'
import { obterMeta, salvarMeta } from '../../api/metas'
import { obterSelicAtual } from '../../api/selic'
import { listarContasBancarias } from '../../api/contasBancarias'
import InsightsCard from './InsightsCard'
import OrcamentoDinamicoCard from './OrcamentoDinamicoCard'
import SaudeFinanceiraGauges from './SaudeFinanceiraGauges'
import type { ContaBancaria } from '../../types'
import { getContasEmRisco, agruparVencimentos } from '../../utils/alertas'
import type { MetaAnual } from '../../types'
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, PieChart, Pie, Cell, Legend, ReferenceLine } from 'recharts'
import { grupoDaCategoria, CORES_GRUPO } from '../../utils/categorias'
import './ClientDashboard.css'

interface Props { clienteIdOverride?: string }

const MONTHS = ['Jan','Fev','Mar','Abr','Mai','Jun','Jul','Ago','Set','Out','Nov','Dez']
const MONTH_NAMES = ['Janeiro','Fevereiro','Março','Abril','Maio','Junho','Julho','Agosto','Setembro','Outubro','Novembro','Dezembro']

function fmtNum(n: number) {
  if (!n) return ''
  return n.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}
function parseBRL(s: string): number {
  return parseFloat(s.replace(/\./g, '').replace(',', '.')) || 0
}

export default function ClientDashboardPage({ clienteIdOverride }: Props) {
  const { user } = useAuth()
  const clienteId = clienteIdOverride ?? user?.usuarioId ?? null
  const { registros, loading } = useRegistros(clienteId)
  const navigate = useNavigate()

  const [contasBancarias, setContasBancarias] = useState<ContaBancaria[]>([])
  const [contaFiltro, setContaFiltro] = useState<string | null>(null)

  useEffect(() => {
    if (!clienteId) return
    listarContasBancarias(clienteId).then(setContasBancarias).catch(console.error)
  }, [clienteId])

  // registros filtrados por conta quando há filtro ativo
  const registrosFiltrados = useMemo(() =>
    contaFiltro ? registros.filter(r => r.contaBancariaId === contaFiltro) : registros,
    [registros, contaFiltro]
  )

  const saldoConsolidado = useMemo(() =>
    contasBancarias.filter(c => c.ativa).reduce((s, c) => s + c.saldoAtual, 0),
    [contasBancarias]
  )

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
  const [editMesInicio, setEditMesInicio] = useState(1)
  const [editPeriodoMeses, setEditPeriodoMeses] = useState(12)
  const [savingMeta, setSavingMeta] = useState(false)
  const [metaMsg, setMetaMsg] = useState('')
  // Meu Sonho / Investimentos
  const [editSonho, setEditSonho] = useState('')
  const [editModoMeta, setEditModoMeta] = useState<'simples' | 'metodo'>('simples')
  const [editValorSonho, setEditValorSonho] = useState('')
  const [editPrazoAnos, setEditPrazoAnos] = useState('')
  const [editTaxaRetorno, setEditTaxaRetorno] = useState('')
  const [editTotalInvestido, setEditTotalInvestido] = useState('')
  const [editMargemPJ, setEditMargemPJ] = useState('')
  const [editIconeSonho, setEditIconeSonho] = useState('')
  const [selic, setSelic] = useState(10.5)
  const [simAporteExtra, setSimAporteExtra] = useState('')

  useEffect(() => {
    obterSelicAtual().then(setSelic).catch(() => setSelic(10.5))
  }, [])

  useEffect(() => {
    if (!clienteId) return
    obterMeta(clienteId, anoAtual)
      .then(m => {
        setMeta(m)
        if (m) {
          setEditReceita(fmtNum(m.metaReceita))
          setEditLucro(fmtNum(m.metaLucro))
          setEditMesInicio(m.mesInicio ?? 1)
          setEditPeriodoMeses(m.periodoMeses ?? 12)
          setEditSonho(m.sonho ?? '')
          setEditModoMeta(m.modoMeta ?? 'simples')
          setEditValorSonho(m.valorSonho ? fmtNum(m.valorSonho) : '')
          setEditPrazoAnos(m.prazoAnos ? String(m.prazoAnos) : '')
          setEditTaxaRetorno(m.taxaRetorno ? String(m.taxaRetorno) : '')
          setEditTotalInvestido(m.totalInvestido ? fmtNum(m.totalInvestido) : '')
          setEditMargemPJ(m.margemPJ ? String(m.margemPJ) : '')
          setEditIconeSonho(m.iconeSonho ?? '')
        }
      })
      .catch(console.error)
  }, [clienteId, anoAtual])

  const doPeriodo = useMemo(() =>
    registrosFiltrados.filter(r => r.data >= de && r.data <= ate),
    [registrosFiltrados, de, ate]
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

  // Projeção por pagamentos cadastrados (contas a receber/pagar pendentes por mês)
  const projecaoPagamentos = useMemo(() => {
    const hoje = todayISO().slice(0, 7)
    const byMonth: Record<string, { receber: number; pagar: number }> = {}
    for (const r of registros) {
      for (const c of r.contasAReceber) {
        if (!c.pago && c.dataVencimento && c.dataVencimento.slice(0, 7) >= hoje) {
          const mes = c.dataVencimento.slice(0, 7)
          if (!byMonth[mes]) byMonth[mes] = { receber: 0, pagar: 0 }
          byMonth[mes].receber += c.valor
        }
      }
      for (const c of r.contasAPagar) {
        if (!c.pago && c.dataVencimento && c.dataVencimento.slice(0, 7) >= hoje) {
          const mes = c.dataVencimento.slice(0, 7)
          if (!byMonth[mes]) byMonth[mes] = { receber: 0, pagar: 0 }
          byMonth[mes].pagar += c.valor
        }
      }
    }
    return Object.entries(byMonth)
      .sort(([a], [b]) => a.localeCompare(b))
      .slice(0, 6)
      .map(([mes, v]) => ({
        mes,
        label: `${MONTHS[parseInt(mes.slice(5)) - 1]}/${mes.slice(0, 4)}`,
        receber: v.receber,
        pagar: v.pagar,
        saldo: v.receber - v.pagar,
      }))
  }, [registros])

  const planejamento = useMemo(() => {
    if (!meta) return []
    const mesInicio = meta.mesInicio ?? 1
    const periodoMeses = meta.periodoMeses ?? 12
    const anoMeta = meta.ano

    const salvoDate = meta.salvoEm ? new Date(meta.salvoEm) : null
    const mesSalvo = salvoDate ? salvoDate.getMonth() + 1 : 1
    const anoSalvo = salvoDate ? salvoDate.getFullYear() : anoMeta

    let remainingReceita = meta.metaReceita
    let remainingLucro = meta.metaLucro

    return Array.from({ length: periodoMeses }, (_, idx) => {
      const mes = ((mesInicio - 1 + idx) % 12) + 1
      const ano = anoMeta + Math.floor((mesInicio - 1 + idx) / 12)
      const remainingMonths = periodoMeses - idx

      const prefixo = `${ano}-${String(mes).padStart(2, '0')}`
      const doMes = registros.filter(r => r.data.startsWith(prefixo))
      const receitaReal = doMes.reduce((s, r) => s + r.entradas.reduce((a, e) => a + e.valor, 0), 0)
      const lucroReal = doMes.reduce((s, r) =>
        s + r.entradas.reduce((a, e) => a + e.valor, 0) - r.saidas.reduce((a, e) => a + e.valor, 0), 0)

      const isPassado = ano < anoAtual || (ano === anoAtual && mes < mesAtual)
      const isAtual = ano === anoAtual && mes === mesAtual
      const isAntesSalvo = salvoDate
        ? (ano < anoSalvo || (ano === anoSalvo && mes < mesSalvo))
        : false

      const targetReceita = remainingReceita / remainingMonths
      const targetLucro = remainingLucro / remainingMonths

      if (isPassado || isAtual) {
        remainingReceita -= receitaReal
        remainingLucro -= lucroReal
      }

      return {
        mes, ano,
        label: `${MONTHS[mes - 1]}/${ano}`,
        targetReceita: isAntesSalvo ? null : targetReceita,
        targetLucro: isAntesSalvo ? null : targetLucro,
        receitaReal: (isPassado || isAtual) ? receitaReal : null,
        lucroReal: (isPassado || isAtual) ? lucroReal : null,
        isAtual,
        isAntesSalvo,
      }
    })
  }, [meta, registros, anoAtual, mesAtual])

  const metaMesAtual = useMemo(() => {
    const linha = planejamento.find(p => p.isAtual)
    if (!linha || linha.lucroReal === null || linha.targetLucro === null) return null
    const alvo = linha.targetLucro
    const real = linha.lucroReal
    const pct = alvo > 0 ? Math.min(100, Math.max(0, (real / alvo) * 100)) : 0
    const faltante = Math.max(0, alvo - real)
    return { alvo, real, pct, faltante, atingida: real >= alvo }
  }, [planejamento])

  const aporteMensal = useMemo(() => {
    const vf = parseBRL(editValorSonho)
    const n = Number(editPrazoAnos) * 12
    const taxa = Number(editTaxaRetorno)
    const investido = parseBRL(editTotalInvestido)
    if (!vf || !n || !taxa) return null
    const i = Math.pow(1 + taxa / 100, 1 / 12) - 1
    const fvAtual = investido * Math.pow(1 + i, n)
    const fvNecessario = vf - fvAtual
    if (fvNecessario <= 0) return 0
    return (fvNecessario * i) / (Math.pow(1 + i, n) - 1)
  }, [editValorSonho, editPrazoAnos, editTaxaRetorno, editTotalInvestido])

  const projecaoSelic = useMemo(() => {
    const base = parseBRL(editTotalInvestido)
    if (!base || !selic) return null
    return {
      dez: base * Math.pow(1 + selic / 100, 10),
      vinte: base * Math.pow(1 + selic / 100, 20),
      trinta: base * Math.pow(1 + selic / 100, 30),
    }
  }, [editTotalInvestido, selic])

  const trajetorias = useMemo(() => {
    const taxa = Number(editTaxaRetorno)
    const prazo = Number(editPrazoAnos)
    const investido = parseBRL(editTotalInvestido)
    if (!prazo || !taxa || !investido) return null
    const ap = aporteMensal ?? 0
    const calcSerie = (taxaAa: number) => {
      const i = Math.pow(1 + Math.max(0, taxaAa) / 100, 1 / 12) - 1
      let p = investido
      const serie = [p]
      for (let ano = 1; ano <= prazo; ano++) {
        for (let m = 0; m < 12; m++) p = p * (1 + i) + ap
        serie.push(p)
      }
      return serie
    }
    const pess = calcSerie(taxa - 3)
    const real = calcSerie(taxa)
    const otim = calcSerie(taxa + 3)
    return Array.from({ length: prazo + 1 }, (_, idx) => ({
      label: idx === 0 ? 'Hoje' : `Ano ${idx}`,
      pessimista: Math.round(pess[idx]),
      realista: Math.round(real[idx]),
      otimista: Math.round(otim[idx]),
    }))
  }, [editTaxaRetorno, editPrazoAnos, editTotalInvestido, aporteMensal])

  const tempoAteMeta = useMemo(() => {
    const vf = parseBRL(editValorSonho)
    const taxa = Number(editTaxaRetorno)
    const investido = parseBRL(editTotalInvestido)
    if (!vf || !taxa || !investido || aporteMensal === null) return null
    if (investido >= vf) return { meses: 0, anos: 0, mesesRest: 0 }
    const i = Math.pow(1 + taxa / 100, 1 / 12) - 1
    let p = investido
    let meses = 0
    const MAX = 600
    while (p < vf && meses < MAX) { p = p * (1 + i) + aporteMensal; meses++ }
    if (meses >= MAX) return null
    return { meses, anos: Math.floor(meses / 12), mesesRest: meses % 12 }
  }, [editValorSonho, editTaxaRetorno, editTotalInvestido, aporteMensal])

  const simTempoComExtra = useMemo(() => {
    if (!simAporteExtra || !tempoAteMeta || tempoAteMeta.meses === 0 || aporteMensal === null) return null
    const vf = parseBRL(editValorSonho)
    const taxa = Number(editTaxaRetorno)
    const investido = parseBRL(editTotalInvestido)
    const extra = parseBRL(simAporteExtra)
    if (!vf || !taxa || !investido || !extra) return null
    const i = Math.pow(1 + taxa / 100, 1 / 12) - 1
    const ap = aporteMensal + extra
    let p = investido
    let meses = 0
    const MAX = 600
    while (p < vf && meses < MAX) { p = p * (1 + i) + ap; meses++ }
    if (meses >= MAX) return null
    const ganhoMeses = tempoAteMeta.meses - meses
    return { meses, anos: Math.floor(meses / 12), mesesRest: meses % 12, ganhoMeses }
  }, [simAporteExtra, tempoAteMeta, editValorSonho, editTaxaRetorno, editTotalInvestido, aporteMensal])

  const fireNumber = useMemo(() => {
    const investido = parseBRL(editTotalInvestido)
    if (!investido || totalSaida <= 0) return null
    const daysInPeriod = Math.max(1,
      (new Date(ate).getTime() - new Date(de).getTime()) / 86400000 + 1
    )
    const despMensal = (totalSaida / daysInPeriod) * 30
    const fireTarget = despMensal * 12 / 0.04
    const pct = Math.min(100, (investido / fireTarget) * 100)
    return { fireTarget, despMensal, pct, atingido: investido >= fireTarget }
  }, [editTotalInvestido, totalSaida, de, ate])

  const atrasadaNaSonho = useMemo(() => {
    if (editModoMeta !== 'metodo' || aporteMensal === null || aporteMensal <= 0) return null
    if (!meta?.salvoEm) return null
    const vf = parseBRL(editValorSonho)
    const prazoTotal = Number(editPrazoAnos) * 12
    const taxa = Number(editTaxaRetorno)
    const investido = parseBRL(editTotalInvestido)
    if (!vf || prazoTotal < 2 || !taxa) return null
    const i = Math.pow(1 + taxa / 100, 1 / 12) - 1
    const dataMeta = new Date(meta.salvoEm)
    const agora = new Date()
    const mesesDecorridos = (agora.getFullYear() - dataMeta.getFullYear()) * 12 + (agora.getMonth() - dataMeta.getMonth())
    if (mesesDecorridos <= 0) return null
    const mesesRestantes = prazoTotal - mesesDecorridos
    if (mesesRestantes <= 1) return null
    const fvCorrigido = investido * Math.pow(1 + i, mesesRestantes)
    const fvNecessario = vf - fvCorrigido
    if (fvNecessario <= 0) return { atrasada: false as const, mesesDecorridos, mesesRestantes }
    const aporteNecessarioAgora = (fvNecessario * i) / (Math.pow(1 + i, mesesRestantes) - 1)
    if (aporteNecessarioAgora <= aporteMensal * 1.1) return { atrasada: false as const, mesesDecorridos, mesesRestantes }
    // Opção 2: manter aporte original, calcular novo prazo
    let p = investido
    let mesesExtra = 0
    const MAX = 600
    while (p < vf && mesesExtra < MAX) { p = p * (1 + i) + aporteMensal; mesesExtra++ }
    return {
      atrasada: true as const,
      mesesDecorridos,
      mesesRestantes,
      aporteNecessarioAgora,
      novoPrazoMeses: mesesExtra,
      aporteOriginal: aporteMensal,
    }
  }, [meta, editModoMeta, editValorSonho, editPrazoAnos, editTaxaRetorno, editTotalInvestido, aporteMensal])

  async function handleSaveMeta() {
    if (!clienteId) return
    setSavingMeta(true)
    setMetaMsg('')
    try {
      const saved = await salvarMeta({
        clienteId,
        ano: anoAtual,
        metaReceita: parseBRL(editReceita),
        metaLucro: parseBRL(editLucro),
        mesInicio: editMesInicio,
        periodoMeses: editPeriodoMeses,
        sonho: editSonho,
        modoMeta: editModoMeta,
        valorSonho: parseBRL(editValorSonho),
        prazoAnos: Number(editPrazoAnos) || 0,
        taxaRetorno: Number(editTaxaRetorno) || 0,
        totalInvestido: parseBRL(editTotalInvestido),
        margemPJ: editMargemPJ ? Number(editMargemPJ) : undefined,
        iconeSonho: editIconeSonho || undefined,
      })
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

      {/* Filtro por conta bancária */}
      {contasBancarias.filter(c => c.ativa).length > 1 && (
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 10, flexWrap: 'wrap' }}>
          <span style={{ fontSize: 12, color: 'var(--tx3)' }}>Conta:</span>
          <button
            type="button"
            onClick={() => setContaFiltro(null)}
            style={{
              padding: '4px 12px', borderRadius: 8, fontSize: 12, cursor: 'pointer',
              border: '1px solid var(--bd)',
              background: contaFiltro === null ? '#0a84ff' : 'var(--bg-card)',
              color: contaFiltro === null ? '#fff' : 'var(--tx1)',
            }}>
            Todas
          </button>
          {contasBancarias.filter(c => c.ativa).map(c => (
            <button
              key={c.id}
              type="button"
              onClick={() => setContaFiltro(c.id)}
              style={{
                padding: '4px 12px', borderRadius: 8, fontSize: 12, cursor: 'pointer',
                border: '1px solid var(--bd)',
                background: contaFiltro === c.id ? '#0a84ff' : 'var(--bg-card)',
                color: contaFiltro === c.id ? '#fff' : 'var(--tx1)',
              }}>
              {c.nome}
            </button>
          ))}
        </div>
      )}

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
        <StatCard
          label={contaFiltro === null ? '💰 Saldo Consolidado' : '💰 Saldo da Conta'}
          value={contaFiltro === null
            ? fmtBRL(saldoConsolidado)
            : fmtBRL(contasBancarias.find(c => c.id === contaFiltro)?.saldoAtual ?? saldoFinal)}
          className="val-blue"
          sub={contaFiltro === null && contasBancarias.filter(c => c.ativa).length > 1
            ? `${contasBancarias.filter(c => c.ativa).length} contas`
            : undefined}
        />
      </div>

      {clienteId && <SaudeFinanceiraGauges clienteId={clienteId} />}
      {clienteId && <OrcamentoDinamicoCard clienteId={clienteId} />}
      {clienteId && <InsightsCard clienteId={clienteId} />}

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
        <h3>🎯 Metas & Investimentos {anoAtual}</h3>

        {/* ── Meu Sonho ── */}
        <div className="meta-sonho-block">
          <label className="meta-field-label">💭 Meu Sonho</label>
          <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
            <input
              type="text"
              className="meta-sonho-input"
              style={{ flex: 1 }}
              placeholder="Ex: Ter R$ 1.000.000 investidos, aposentadoria antecipada..."
              value={editSonho}
              onChange={e => setEditSonho(e.target.value)}
            />
            <div style={{ display: 'flex', gap: 4 }}>
              {(['🚗', '✈️', '🏠', '⭐'] as const).map(icone => (
                <button
                  key={icone}
                  type="button"
                  onClick={() => setEditIconeSonho(editIconeSonho === icone ? '' : icone)}
                  style={{
                    fontSize: 20, padding: '4px 6px', borderRadius: 8, cursor: 'pointer',
                    border: `2px solid ${editIconeSonho === icone ? '#0a84ff' : 'var(--bd)'}`,
                    background: editIconeSonho === icone ? 'rgba(10,132,255,.12)' : 'var(--bg-card)',
                  }}
                >{icone}</button>
              ))}
            </div>
          </div>
          <div className="meta-modo-tabs">
            <button
              className={`meta-modo-btn ${editModoMeta === 'simples' ? 'active' : ''}`}
              onClick={() => setEditModoMeta('simples')}>
              Meta simples
            </button>
            <button
              className={`meta-modo-btn ${editModoMeta === 'metodo' ? 'active' : ''}`}
              onClick={() => setEditModoMeta('metodo')}>
              Meta com método
            </button>
          </div>

          {editModoMeta === 'metodo' && (
            <div className="meta-metodo-fields">
              <div className="meta-metodo-row">
                <div className="meta-metodo-field">
                  <label className="meta-field-label">Valor do sonho</label>
                  <div className="val-input-wrap">
                    <span className="val-prefix">R$</span>
                    <input type="text" inputMode="decimal" value={editValorSonho}
                      onChange={e => setEditValorSonho(e.target.value)}
                      onBlur={() => { const n = parseBRL(editValorSonho); if (n) setEditValorSonho(fmtNum(n)) }}
                      placeholder="1.000.000,00" />
                  </div>
                </div>
                <div className="meta-metodo-field">
                  <label className="meta-field-label">Já investido</label>
                  <div className="val-input-wrap">
                    <span className="val-prefix">R$</span>
                    <input type="text" inputMode="decimal" value={editTotalInvestido}
                      onChange={e => setEditTotalInvestido(e.target.value)}
                      onBlur={() => { const n = parseBRL(editTotalInvestido); if (n) setEditTotalInvestido(fmtNum(n)) }}
                      placeholder="0,00" />
                  </div>
                </div>
                <div className="meta-metodo-field">
                  <label className="meta-field-label">Prazo (anos)</label>
                  <input type="text" inputMode="numeric" value={editPrazoAnos}
                    onChange={e => setEditPrazoAnos(e.target.value)} placeholder="10"
                    style={{ padding: '8px 10px', background: 'var(--bg)', border: '1px solid var(--bd)', borderRadius: 8, color: 'var(--tx1)', fontSize: 14, width: '100%' }} />
                </div>
                <div className="meta-metodo-field">
                  <label className="meta-field-label">Taxa retorno (% a.a.) <span style={{ color: 'var(--tx3)', fontSize: 11 }}>SELIC: {selic.toFixed(1)}%</span></label>
                  <input type="text" inputMode="decimal" value={editTaxaRetorno}
                    onChange={e => setEditTaxaRetorno(e.target.value)} placeholder={selic.toFixed(1)}
                    style={{ padding: '8px 10px', background: 'var(--bg)', border: '1px solid var(--bd)', borderRadius: 8, color: 'var(--tx1)', fontSize: 14, width: '100%' }} />
                </div>
                <div className="meta-metodo-field">
                  <label className="meta-field-label">
                    Margem PJ (%) <span style={{ color: 'var(--tx3)', fontSize: 11 }}>% da receita que vira aporte</span>
                  </label>
                  <input type="text" inputMode="decimal" value={editMargemPJ}
                    onChange={e => setEditMargemPJ(e.target.value)} placeholder="20"
                    style={{ padding: '8px 10px', background: 'var(--bg)', border: '1px solid var(--bd)', borderRadius: 8, color: 'var(--tx1)', fontSize: 14, width: '100%' }} />
                </div>
              </div>

              {aporteMensal !== null && (
                <div className="meta-aporte-result">
                  <div style={{ fontSize: 12, color: 'var(--tx3)', marginBottom: 4 }}>
                    Para atingir {fmtBRL(parseBRL(editValorSonho))} em {editPrazoAnos} anos com {editTaxaRetorno}% a.a.:
                  </div>
                  {aporteMensal === 0
                    ? <div style={{ fontSize: 16, fontWeight: 700, color: '#34c759' }}>✅ Seu capital atual já atinge a meta no prazo!</div>
                    : <>
                        <div style={{ fontSize: 22, fontWeight: 800, color: '#0a84ff' }}>
                          Aporte mensal necessário: <span>{fmtBRL(aporteMensal)}</span>
                        </div>
                        {editMargemPJ && Number(editMargemPJ) > 0 && (
                          <div style={{ fontSize: 13, color: 'var(--tx3)', marginTop: 6 }}>
                            💼 Com margem PJ de {editMargemPJ}%, você precisa faturar{' '}
                            <strong style={{ color: 'var(--tx1)' }}>
                              {fmtBRL(aporteMensal / (Number(editMargemPJ) / 100))}
                            </strong>{' '}
                            adicionalmente por mês.
                          </div>
                        )}
                      </>
                  }
                </div>
              )}

              {tempoAteMeta !== null && (
                <div className="meta-tempo-meta">
                  {tempoAteMeta.meses === 0
                    ? <span style={{ color: '#34c759', fontWeight: 700 }}>✅ Você já atingiu sua meta!</span>
                    : <>
                        <span>⏱ Tempo estimado até a meta: </span>
                        <strong style={{ color: '#34c759' }}>
                          {tempoAteMeta.anos > 0 ? `${tempoAteMeta.anos} ano${tempoAteMeta.anos > 1 ? 's' : ''}` : ''}
                          {tempoAteMeta.anos > 0 && tempoAteMeta.mesesRest > 0 ? ' e ' : ''}
                          {tempoAteMeta.mesesRest > 0 ? `${tempoAteMeta.mesesRest} mês${tempoAteMeta.mesesRest > 1 ? 'es' : ''}` : ''}
                        </strong>
                      </>
                  }
                </div>
              )}

              {aporteMensal !== null && aporteMensal > 0 && (
                <div className="meta-simulador">
                  <label className="meta-field-label">Simulador — e se eu aportar R$ a mais por mês?</label>
                  <div className="meta-simulador-row">
                    <div className="val-input-wrap" style={{ flex: 1, minWidth: 130 }}>
                      <span className="val-prefix">R$</span>
                      <input type="text" inputMode="decimal" value={simAporteExtra}
                        onChange={e => setSimAporteExtra(e.target.value)}
                        onBlur={() => { const n = parseBRL(simAporteExtra); if (n) setSimAporteExtra(fmtNum(n)) }}
                        placeholder="500,00" />
                    </div>
                    {simTempoComExtra && (
                      <div className="meta-sim-result">
                        {simTempoComExtra.ganhoMeses > 0
                          ? <>
                              Chegaria em{' '}
                              <strong style={{ color: '#0a84ff' }}>
                                {simTempoComExtra.anos > 0 ? `${simTempoComExtra.anos}a ` : ''}{simTempoComExtra.mesesRest}m
                              </strong>
                              {' '}·{' '}
                              <span style={{ color: '#34c759' }}>ganho de {simTempoComExtra.ganhoMeses} meses!</span>
                            </>
                          : 'Pouca diferença no prazo'
                        }
                      </div>
                    )}
                  </div>
                </div>
              )}

              {atrasadaNaSonho?.atrasada && (
                <div className="meta-recovery-card">
                  <div className="meta-recovery-title">⚠️ Meta com atraso detectado</div>
                  <p className="meta-recovery-desc">
                    Passaram-se <strong>{atrasadaNaSonho.mesesDecorridos} meses</strong> desde que você definiu esta meta.
                    Seu aporte necessário subiu. Escolha como recuperar:
                  </p>
                  <div className="meta-recovery-options">
                    <div className="meta-recovery-option">
                      <div className="meta-recovery-option-label">Opção 1 · Manter prazo</div>
                      <div className="meta-recovery-option-val">
                        {fmtBRL(atrasadaNaSonho.aporteNecessarioAgora)}
                        <span style={{ fontSize: 13, fontWeight: 400 }}>/mês</span>
                      </div>
                      <div className="meta-recovery-option-desc">Novo aporte para chegar à meta no prazo original</div>
                    </div>
                    <div className="meta-recovery-sep">ou</div>
                    <div className="meta-recovery-option">
                      <div className="meta-recovery-option-label">Opção 2 · Manter aporte</div>
                      <div className="meta-recovery-option-val">
                        +{Math.ceil(Math.max(0, atrasadaNaSonho.novoPrazoMeses - atrasadaNaSonho.mesesRestantes) / 12 * 10) / 10}a
                        <span style={{ fontSize: 13, fontWeight: 400 }}> a mais</span>
                      </div>
                      <div className="meta-recovery-option-desc">
                        Aportar {fmtBRL(atrasadaNaSonho.aporteOriginal)}/mês → meta em{' '}
                        {Math.floor(atrasadaNaSonho.novoPrazoMeses / 12)}a{' '}
                        {atrasadaNaSonho.novoPrazoMeses % 12}m
                      </div>
                    </div>
                  </div>
                </div>
              )}
            </div>
          )}
        </div>

        <div className="meta-period-row">
          <div className="meta-period-field">
            <label>Mês início</label>
            <select value={editMesInicio} onChange={e => setEditMesInicio(Number(e.target.value))}>
              {MONTH_NAMES.map((m, i) => <option key={i + 1} value={i + 1}>{m}</option>)}
            </select>
          </div>
          <div className="meta-period-field">
            <label>Duração</label>
            <select value={editPeriodoMeses} onChange={e => setEditPeriodoMeses(Number(e.target.value))}>
              {Array.from({ length: 12 }, (_, i) => i + 1).map(n => (
                <option key={n} value={n}>{n} {n === 1 ? 'mês' : 'meses'}</option>
              ))}
            </select>
          </div>
          <span style={{ fontSize: 12, color: 'var(--tx3)' }}>
            {MONTH_NAMES[editMesInicio - 1]} → {MONTH_NAMES[((editMesInicio - 1 + editPeriodoMeses - 1) % 12)]}
          </span>
        </div>

        <div className="meta-row">
          <label>Meta de Receita</label>
          <div className="val-input-wrap">
            <span className="val-prefix">R$</span>
            <input type="text" inputMode="decimal" value={editReceita}
              onChange={e => setEditReceita(e.target.value)}
              onBlur={() => { const n = parseBRL(editReceita); if (n) setEditReceita(fmtNum(n)) }}
              placeholder="0,00" />
          </div>
        </div>
        <div className="meta-row">
          <label>Meta de Lucro</label>
          <div className="val-input-wrap">
            <span className="val-prefix">R$</span>
            <input type="text" inputMode="decimal" value={editLucro}
              onChange={e => setEditLucro(e.target.value)}
              onBlur={() => { const n = parseBRL(editLucro); if (n) setEditLucro(fmtNum(n)) }}
              placeholder="0,00" />
          </div>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginTop: 4 }}>
          <button className="btn-meta" onClick={handleSaveMeta} disabled={savingMeta}>
            {savingMeta ? 'Salvando...' : '💾 Salvar metas'}
          </button>
          {metaMsg && <span style={{ fontSize: 13, color: '#34c759' }}>{metaMsg}</span>}
        </div>
        {meta?.salvoEm && (
          <div className="meta-salvo-em">
            Projeção calculada a partir de {new Date(meta.salvoEm).toLocaleDateString('pt-BR', { day: '2-digit', month: 'long', year: 'numeric' })}
          </div>
        )}
      </div>

      {trajetorias && editModoMeta === 'metodo' && (
        <div className="meta-card">
          <h3>📊 Trajetória do Patrimônio</h3>
          <p style={{ fontSize: 12, color: 'var(--tx3)', marginBottom: 10 }}>
            3 cenários: <span style={{ color: '#ff6b6b' }}>pessimista ({(Number(editTaxaRetorno) - 3).toFixed(1)}%)</span>,{' '}
            <span style={{ color: '#0a84ff' }}>realista ({editTaxaRetorno}%)</span> e{' '}
            <span style={{ color: '#34c759' }}>otimista ({(Number(editTaxaRetorno) + 3).toFixed(1)}% a.a.)</span>
          </p>
          <div style={{ height: 240 }}>
            <ResponsiveContainer width="100%" height="100%">
              <LineChart data={trajetorias}>
                <CartesianGrid strokeDasharray="3 3" stroke="var(--bd)" />
                <XAxis dataKey="label" stroke="var(--tx3)" tick={{ fontSize: 11 }} />
                <YAxis stroke="var(--tx3)" tick={{ fontSize: 11 }} tickFormatter={v => `R$${(Number(v) / 1000).toFixed(0)}k`} width={60} />
                <Tooltip formatter={(v) => typeof v === 'number' ? fmtBRL(v) : String(v)} contentStyle={{ background: 'var(--bg-card)', border: '1px solid var(--bd)' }} />
                <Legend />
                <Line type="monotone" dataKey="pessimista" stroke="#ff6b6b" strokeWidth={2} dot={false} name="Pessimista" />
                <Line type="monotone" dataKey="realista" stroke="#0a84ff" strokeWidth={2} dot={false} name="Realista" />
                <Line type="monotone" dataKey="otimista" stroke="#34c759" strokeWidth={2} dot={false} name="Otimista" />
                {Number(editValorSonho) > 0 && (
                  <ReferenceLine y={Number(editValorSonho)} stroke="#ff9500" strokeDasharray="5 5"
                    label={{ value: 'Meta', fill: '#ff9500', fontSize: 11, position: 'insideTopRight' }} />
                )}
              </LineChart>
            </ResponsiveContainer>
          </div>
        </div>
      )}

      {projecaoSelic && (
        <div className="meta-card">
          <h3>💹 Projeção à SELIC <span style={{ fontSize: 12, color: 'var(--tx3)', fontWeight: 400 }}>({selic.toFixed(2)}% a.a.)</span></h3>
          <p style={{ fontSize: 12, color: 'var(--tx3)', marginBottom: 12 }}>
            Capital de {fmtBRL(Number(editTotalInvestido))} composto à SELIC, sem aportes adicionais.
          </p>
          <div className="stats-grid">
            <StatCard label="📅 Em 10 anos" value={fmtBRL(projecaoSelic.dez)} className="val-blue" />
            <StatCard label="📅 Em 20 anos" value={fmtBRL(projecaoSelic.vinte)} className="val-blue" />
            <StatCard label="📅 Em 30 anos" value={fmtBRL(projecaoSelic.trinta)} className="val-blue" />
          </div>
        </div>
      )}

      {fireNumber && (
        <div className="meta-card">
          <h3>🔥 Indicador FIRE <span style={{ fontSize: 12, color: 'var(--tx3)', fontWeight: 400 }}>(Financial Independence, Retire Early)</span></h3>
          <p style={{ fontSize: 12, color: 'var(--tx3)', marginBottom: 12 }}>
            Regra dos 4%: patrimônio necessário = despesas mensais × 12 ÷ 4%. Baseado no período selecionado.
          </p>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13, color: 'var(--tx3)', marginBottom: 6 }}>
            <span>Despesa mensal estimada: <strong style={{ color: 'var(--tx1)' }}>{fmtBRL(fireNumber.despMensal)}</strong></span>
            <span>Meta FIRE: <strong style={{ color: 'var(--tx1)' }}>{fmtBRL(fireNumber.fireTarget)}</strong></span>
          </div>
          <div style={{ height: 14, background: 'var(--bd)', borderRadius: 8, overflow: 'hidden', marginBottom: 8 }}>
            <div style={{ width: `${fireNumber.pct}%`, height: '100%', background: fireNumber.atingido ? '#34c759' : '#0a84ff', transition: 'width .3s' }} />
          </div>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 14, fontWeight: 600 }}>
            <span style={{ color: 'var(--tx3)' }}>{fireNumber.pct.toFixed(1)}% da independência financeira</span>
            <span style={{ color: fireNumber.atingido ? '#34c759' : '#ff9500' }}>
              {fireNumber.atingido
                ? '🎉 FIRE atingido!'
                : `Faltam ${fmtBRL(fireNumber.fireTarget - Number(editTotalInvestido))}`}
            </span>
          </div>
        </div>
      )}

      {meta && planejamento.length > 0 && (
        <div className="meta-card">
          <h3>📅 Projeção Mês a Mês — {MONTH_NAMES[meta.mesInicio - 1]} a {MONTH_NAMES[((meta.mesInicio - 1 + meta.periodoMeses - 1) % 12)]} / {meta.ano}</h3>
          <p style={{ fontSize: 12, color: 'var(--tx3)', marginBottom: 10 }}>
            Redistribuição automática do restante nos meses seguintes. Projeção ativa a partir de{' '}
            {meta.salvoEm ? new Date(meta.salvoEm).toLocaleDateString('pt-BR', { month: 'long', year: 'numeric' }) : '—'}.
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
                <tr key={`${p.ano}-${p.mes}`} className={p.isAtual ? 'atual' : ''}>
                  <td>{p.label}</td>
                  <td>
                    {p.targetReceita !== null
                      ? fmtBRL(p.targetReceita)
                      : <span className="proj">—</span>}
                  </td>
                  <td>
                    {p.receitaReal !== null
                      ? <span className={p.targetReceita !== null && p.receitaReal >= p.targetReceita ? 'ok' : 'nok'}>{fmtBRL(p.receitaReal)}</span>
                      : <span className="proj">—</span>}
                  </td>
                  <td>
                    {p.targetLucro !== null
                      ? fmtBRL(p.targetLucro)
                      : <span className="proj">—</span>}
                  </td>
                  <td>
                    {p.lucroReal !== null
                      ? <span className={p.targetLucro !== null && p.lucroReal >= p.targetLucro ? 'ok' : 'nok'}>{fmtBRL(p.lucroReal)}</span>
                      : <span className="proj">—</span>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {projecaoPagamentos.length > 0 && (
        <div className="meta-card">
          <h3>📋 Projeção por Pagamentos Cadastrados</h3>
          <p style={{ fontSize: 12, color: 'var(--tx3)', marginBottom: 10 }}>
            Contas a receber e a pagar pendentes, agrupadas por mês de vencimento.
          </p>
          <table className="plan-table">
            <thead>
              <tr>
                <th>Mês</th>
                <th>A Receber</th>
                <th>A Pagar</th>
                <th>Saldo Projetado</th>
              </tr>
            </thead>
            <tbody>
              {projecaoPagamentos.map(p => (
                <tr key={p.mes}>
                  <td>{p.label}</td>
                  <td className="ok">{fmtBRL(p.receber)}</td>
                  <td><span style={{ color: '#ff6b6b' }}>{fmtBRL(p.pagar)}</span></td>
                  <td className={p.saldo >= 0 ? 'ok' : 'nok'}>{fmtBRL(p.saldo)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  )
}
