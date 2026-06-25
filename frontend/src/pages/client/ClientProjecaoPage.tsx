import { useState, useEffect, useCallback, useMemo } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { obterProjecao } from '../../api/projecao'
import { listarContasBancarias } from '../../api/contasBancarias'
import { fmtBRL } from '../../utils/format'
import type { Projecao, ProjecaoDia } from '../../api/projecao'
import type { ContaBancaria } from '../../types'
import {
  AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip,
  ResponsiveContainer, ReferenceLine,
} from 'recharts'
import './ClientProjecao.css'

type Janela = 30 | 60 | 90

const MESES_ABREV = ['Jan','Fev','Mar','Abr','Mai','Jun','Jul','Ago','Set','Out','Nov','Dez']

function fmtDataCurta(iso: string): string {
  const d = new Date(iso + 'T12:00:00')
  return `${String(d.getDate()).padStart(2,'0')}/${MESES_ABREV[d.getMonth()]}`
}

function fmtDataCompleta(iso: string): string {
  const d = new Date(iso + 'T12:00:00')
  return `${String(d.getDate()).padStart(2,'0')}/${String(d.getMonth()+1).padStart(2,'0')}/${d.getFullYear()}`
}

// Agrupa dias por semana ISO para a lista de detalhes (evita 90 linhas abertas)
function agruparPorSemana(dias: ProjecaoDia[]): { label: string; dias: ProjecaoDia[] }[] {
  const grupos: Record<string, ProjecaoDia[]> = {}
  dias.forEach(dia => {
    const d = new Date(dia.data + 'T12:00:00')
    // Usa a segunda-feira da semana como chave
    const dow = d.getDay() === 0 ? 6 : d.getDay() - 1
    const seg = new Date(d); seg.setDate(d.getDate() - dow)
    const key = seg.toISOString().slice(0, 10)
    if (!grupos[key]) grupos[key] = []
    grupos[key].push(dia)
  })
  return Object.entries(grupos)
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([key, ds]) => ({
      label: `Semana de ${fmtDataCompleta(key)}`,
      dias: ds,
    }))
}

export default function ClientProjecaoPage() {
  const { user } = useAuth()
  const clienteId = user?.usuarioId ?? ''

  const [janela, setJanela] = useState<Janela>(30)
  const [contaFiltro, setContaFiltro] = useState('')
  const [contas, setContas] = useState<ContaBancaria[]>([])
  const [projecao, setProjecao] = useState<Projecao | null>(null)
  const [loading, setLoading] = useState(false)
  const [erro, setErro] = useState('')
  const [expandidos, setExpandidos] = useState<Record<string, boolean>>({})

  useEffect(() => {
    if (!clienteId) return
    listarContasBancarias(clienteId)
      .then(cs => setContas(cs.filter(c => c.ativa)))
      .catch(() => {})
  }, [clienteId])

  const carregar = useCallback(async () => {
    if (!clienteId) return
    setLoading(true); setErro('')
    try {
      const data = await obterProjecao(clienteId, janela, contaFiltro || undefined)
      setProjecao(data)
      setExpandidos({})
    } catch (e: unknown) {
      setErro(e instanceof Error ? e.message : 'Erro ao carregar projeção.')
    } finally {
      setLoading(false)
    }
  }, [clienteId, janela, contaFiltro])

  useEffect(() => { carregar() }, [carregar])

  // Dados para o gráfico — amostrar a cada N dias para não sobrecarregar
  const dadosGrafico = useMemo(() => {
    if (!projecao) return []
    const passo = janela === 30 ? 1 : janela === 60 ? 2 : 3
    return projecao.dias
      .filter((_, i) => i % passo === 0 || i === projecao.dias.length - 1)
      .map(d => ({
        data: fmtDataCurta(d.data),
        saldo: d.saldoFim,
        negativo: d.saldoNegativo,
      }))
  }, [projecao, janela])

  const diasComMovimento = useMemo(
    () => projecao?.dias.filter(d => d.entradas.length > 0 || d.saidas.length > 0) ?? [],
    [projecao],
  )

  const semanas = useMemo(
    () => janela > 30 ? agruparPorSemana(diasComMovimento) : null,
    [diasComMovimento, janela],
  )

  const temNegativo = projecao?.dias.some(d => d.saldoNegativo) ?? false
  const menorSaldo  = projecao ? Math.min(...projecao.dias.map(d => d.saldoFim)) : 0
  const maiorSaldo  = projecao ? Math.max(...projecao.dias.map(d => d.saldoFim)) : 0

  function toggleDia(key: string) {
    setExpandidos(prev => ({ ...prev, [key]: !prev[key] }))
  }

  return (
    <>
      {/* Cabeçalho */}
      <div className="pj-header">
        <div>
          <h2 className="pj-titulo">🔭 Projeção de Fluxo de Caixa</h2>
          <div className="pj-subtitulo">Simulação de saldo futuro · apenas leitura</div>
        </div>
      </div>

      {/* Controles */}
      <div className="pj-controles">
        <div className="pj-janela-tabs">
          {([30, 60, 90] as Janela[]).map(j => (
            <button
              key={j}
              className={`pj-janela-btn${janela === j ? ' active' : ''}`}
              onClick={() => setJanela(j)}
            >
              {j} dias
            </button>
          ))}
        </div>
        {contas.length > 1 && (
          <select className="pj-select" value={contaFiltro} onChange={e => setContaFiltro(e.target.value)}>
            <option value="">Todas as contas</option>
            {contas.map(c => <option key={c.id} value={c.id}>{c.nome}</option>)}
          </select>
        )}
      </div>

      {loading && <p className="pj-loading">Calculando projeção...</p>}
      {erro    && <p className="pj-erro">{erro}</p>}

      {projecao && !loading && (
        <>
          {/* Cards de resumo */}
          <div className="pj-cards">
            <div className="pj-card">
              <span className="pj-card-label">Saldo Atual</span>
              <span className={`pj-card-val ${projecao.saldoAtual >= 0 ? 'val-green' : 'val-red'}`}>
                {fmtBRL(projecao.saldoAtual)}
              </span>
            </div>
            <div className="pj-card">
              <span className="pj-card-label">Saldo em {janela} dias</span>
              <span className={`pj-card-val ${(projecao.dias.at(-1)?.saldoFim ?? 0) >= 0 ? 'val-green' : 'val-red'}`}>
                {fmtBRL(projecao.dias.at(-1)?.saldoFim ?? 0)}
              </span>
            </div>
            <div className="pj-card">
              <span className="pj-card-label">Pior saldo projetado</span>
              <span className={`pj-card-val ${menorSaldo >= 0 ? 'val-green' : 'val-red'}`}>
                {fmtBRL(menorSaldo)}
              </span>
            </div>
            <div className="pj-card">
              <span className="pj-card-label">Melhor saldo projetado</span>
              <span className="pj-card-val val-green">{fmtBRL(maiorSaldo)}</span>
            </div>
          </div>

          {/* Alerta de saldo negativo */}
          {temNegativo && (
            <div className="pj-alerta">
              ⚠️ Risco de caixa: saldo projetado fica negativo em algum ponto do período.
            </div>
          )}

          {/* Gráfico */}
          <div className="pj-grafico-container">
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={dadosGrafico} margin={{ top: 8, right: 8, bottom: 0, left: 8 }}>
                <defs>
                  <linearGradient id="gradSaldo" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%"  stopColor="#0a84ff" stopOpacity={0.25} />
                    <stop offset="95%" stopColor="#0a84ff" stopOpacity={0.02} />
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" stroke="var(--bd)" />
                <XAxis dataKey="data" stroke="var(--tx3)" tick={{ fontSize: 11 }} interval="preserveStartEnd" />
                <YAxis
                  stroke="var(--tx3)"
                  tick={{ fontSize: 11 }}
                  tickFormatter={v => `R$${(v / 1000).toFixed(0)}k`}
                  width={60}
                />
                <Tooltip
                  formatter={(v) => typeof v === 'number' ? [fmtBRL(v), 'Saldo'] : [String(v)]}
                  contentStyle={{ background: 'var(--bg-card)', border: '1px solid var(--bd)', fontSize: 12 }}
                />
                {temNegativo && <ReferenceLine y={0} stroke="#ff3b30" strokeDasharray="4 2" label={{ value: 'R$ 0', fill: '#ff3b30', fontSize: 11 }} />}
                <Area
                  type="monotone"
                  dataKey="saldo"
                  name="Saldo"
                  stroke="#0a84ff"
                  strokeWidth={2}
                  fill="url(#gradSaldo)"
                  dot={false}
                  activeDot={{ r: 4 }}
                />
              </AreaChart>
            </ResponsiveContainer>
          </div>

          {/* Lista de movimentações */}
          <h3 className="pj-lista-titulo">Movimentações previstas</h3>

          {diasComMovimento.length === 0 && (
            <p className="pj-vazio">Nenhuma entrada ou saída prevista no período.</p>
          )}

          {/* 30 dias: lista diária | 60/90 dias: agrupada por semana */}
          {janela === 30 || !semanas ? (
            <div className="pj-lista">
              {diasComMovimento.map(dia => (
                <DiaItem
                  key={dia.data}
                  dia={dia}
                  expanded={!!expandidos[dia.data]}
                  onToggle={() => toggleDia(dia.data)}
                />
              ))}
            </div>
          ) : (
            <div className="pj-lista">
              {semanas.map(sem => (
                <div key={sem.label} className="pj-semana">
                  <button className="pj-semana-header" onClick={() => toggleDia(sem.label)}>
                    <span>{expandidos[sem.label] ? '▾' : '▸'} {sem.label}</span>
                    <span className="pj-semana-resumo">
                      {sem.dias.reduce((s, d) => s + d.entradas.length + d.saidas.length, 0)} lançamento(s)
                    </span>
                  </button>
                  {expandidos[sem.label] && sem.dias.map(dia => (
                    <DiaItem
                      key={dia.data}
                      dia={dia}
                      expanded={!!expandidos[dia.data + '_inner']}
                      onToggle={() => toggleDia(dia.data + '_inner')}
                    />
                  ))}
                </div>
              ))}
            </div>
          )}
        </>
      )}
    </>
  )
}

interface DiaItemProps {
  dia: ProjecaoDia
  expanded: boolean
  onToggle: () => void
}

function DiaItem({ dia, expanded, onToggle }: DiaItemProps) {
  const temMovimento = dia.entradas.length > 0 || dia.saidas.length > 0
  if (!temMovimento) return null

  return (
    <div className={`pj-dia${dia.saldoNegativo ? ' pj-dia-negativo' : ''}`}>
      <button className="pj-dia-header" onClick={onToggle}>
        <span className="pj-dia-data">{fmtDataCompleta(dia.data)}</span>
        <span className="pj-dia-resumo">
          {dia.entradas.length > 0 && (
            <span className="val-green">+{fmtBRL(dia.totalEntradas)}</span>
          )}
          {dia.saidas.length > 0 && (
            <span className="val-red"> -{fmtBRL(dia.totalSaidas)}</span>
          )}
        </span>
        <span className={`pj-dia-saldo ${dia.saldoNegativo ? 'val-red' : 'val-green'}`}>
          = {fmtBRL(dia.saldoFim)}
        </span>
        <span className="pj-expand">{expanded ? '▾' : '▸'}</span>
      </button>

      {expanded && (
        <div className="pj-dia-detalhe">
          {dia.entradas.map((e, i) => (
            <div key={i} className="pj-item pj-item-entrada">
              <span className="pj-item-origem pj-origem-rec">{e.origem === 'Recorrente' ? '↺' : '📋'}</span>
              <span className="pj-item-desc">{e.descricao}</span>
              {e.categoria && <span className="pj-item-cat">{e.categoria}</span>}
              <span className="pj-item-val val-green">+{fmtBRL(e.valor)}</span>
            </div>
          ))}
          {dia.saidas.map((s, i) => (
            <div key={i} className="pj-item pj-item-saida">
              <span className="pj-item-origem pj-origem-rec">{s.origem === 'Recorrente' ? '↺' : '📋'}</span>
              <span className="pj-item-desc">{s.descricao}</span>
              {s.categoria && <span className="pj-item-cat">{s.categoria}</span>}
              <span className="pj-item-val val-red">-{fmtBRL(s.valor)}</span>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
