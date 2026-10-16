import { useState, useEffect, useCallback, useMemo } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { obterPrevistoRealizado, obterHistoricoItemPrevistoRealizado } from '../../api/relatorios'
import type { PrevistoRealizado, ItemPrevistoRealizado, HistoricoItemPrevistoRealizado, StatusPrevistoRealizado } from '../../api/relatorios'
import { fmtBRL, fmtBRLCompacto, fmtLidoEm } from '../../utils/format'
import { MvPage, MvHero, MvSectionLabel, MvKpi, MvKpiGrid, MvTabs, MvDetails, prefereMovimentoReduzido } from '../../components/mv'
import {
  AreaChart, Area, CartesianGrid, XAxis, YAxis, Tooltip, ResponsiveContainer,
  LineChart, Line,
} from 'recharts'
import './ClientProjecao.css'

const MESES_ABREV = ['Jan','Fev','Mar','Abr','Mai','Jun','Jul','Ago','Set','Out','Nov','Dez']

type Janela = 1 | 3 | 6

function mesLabel(mes: string): string {
  const [ano, m] = mes.split('-')
  return `${MESES_ABREV[Number(m) - 1]}/${ano.slice(2)}`
}

function diaLabel(data: string): string {
  return data.slice(8, 10)
}

function fmtVariacao(fracao: number): string {
  const sinal = fracao >= 0 ? '+' : ''
  return `${sinal}${(fracao * 100).toLocaleString('pt-BR', { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%`
}

const STATUS_LABEL: Record<StatusPrevistoRealizado, string> = {
  Realizado: 'Realizado', Parcial: 'Parcial', NaoAconteceu: 'Previsto', Atrasado: 'Atrasado',
}
const STATUS_TONE: Record<StatusPrevistoRealizado, 'good' | 'warn' | 'neutral' | 'bad'> = {
  Realizado: 'good', Parcial: 'warn', NaoAconteceu: 'neutral', Atrasado: 'bad',
}

function csvEscape(valor: string): string {
  return /[",;\n]/.test(valor) ? `"${valor.replace(/"/g, '""')}"` : valor
}

function exportarCsv(itens: ItemPrevistoRealizado[]) {
  const linhasCsv = [['Tipo', 'Nome', 'Categoria', 'Previsto', 'Realizado', 'Diferença', 'Diferença %', 'Status']]
  for (const item of itens) {
    linhasCsv.push([
      item.tipo, item.nome, item.categoria ?? '',
      item.previsto.toFixed(2), item.realizado.toFixed(2), item.diferenca.toFixed(2),
      item.diferencaPercentual != null ? (item.diferencaPercentual * 100).toFixed(1) : '',
      STATUS_LABEL[item.status],
    ])
  }
  const csv = linhasCsv.map(l => l.map(csvEscape).join(';')).join('\n')
  const blob = new Blob([`﻿${csv}`], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `previsto_x_realizado.csv`
  a.click()
  URL.revokeObjectURL(url)
}

export default function ClientPrevistoRealizadoPage() {
  const { user } = useAuth()
  const clienteId = user?.usuarioId ?? ''

  const [janela, setJanela] = useState<Janela>(1)
  const [filtroTipo, setFiltroTipo] = useState<'Pagar' | 'Receber'>('Receber')
  const [incluirAvulsosVinculados, setIncluirAvulsosVinculados] = useState(false)
  const [dados, setDados] = useState<PrevistoRealizado | null>(null)
  const [loading, setLoading] = useState(false)
  const [erro, setErro] = useState('')

  const [itemAberto, setItemAberto] = useState<string | null>(null)
  const [historico, setHistorico] = useState<HistoricoItemPrevistoRealizado | null>(null)
  const [carregandoHistorico, setCarregandoHistorico] = useState(false)

  const carregar = useCallback(async () => {
    if (!clienteId) return
    setLoading(true); setErro('')
    try {
      setDados(await obterPrevistoRealizado(clienteId, janela, incluirAvulsosVinculados))
    } catch (e: unknown) {
      setErro(e instanceof Error ? e.message : 'Erro ao carregar relatório.')
    } finally {
      setLoading(false)
    }
  }, [clienteId, janela, incluirAvulsosVinculados])

  useEffect(() => { carregar() }, [carregar])

  // Item 7, ponto 2: hero de impacto no caixa — sempre considera os dois tipos juntos (receber E
  // pagar), independente do filtro "Só entradas/Só saídas" escolhido pra lista/gráfico abaixo.
  const itensReceber = useMemo(() => dados?.itens.filter(i => i.tipo === 'Receber') ?? [], [dados])
  const itensPagar = useMemo(() => dados?.itens.filter(i => i.tipo === 'Pagar') ?? [], [dados])
  const diferencaReceber = itensReceber.reduce((s, i) => s + i.diferenca, 0)
  const diferencaPagar = itensPagar.reduce((s, i) => s + i.diferenca, 0)
  const impactoNoCaixa = diferencaReceber - diferencaPagar

  const itensFiltrados = useMemo(
    () => (dados?.itens ?? []).filter(i => i.tipo === filtroTipo).sort((a, b) => Math.abs(b.diferenca) - Math.abs(a.diferenca)),
    [dados, filtroTipo],
  )

  const porDia = janela === 1

  // Área vermelha (item 7, ponto 3): "furo" visível quando o realizado acumulado fica abaixo do
  // previsto nas entradas, ou acima nas saídas — mesma técnica de faixa [min,max] já usada na
  // Projeção (zero de altura quando favorável, visível só quando desfavorável).
  const dadosGrafico = useMemo(() => {
    const acumulado = filtroTipo === 'Receber' ? dados?.acumuladoReceber ?? [] : dados?.acumuladoPagar ?? []
    return acumulado.map(p => {
      const desfavoravel = filtroTipo === 'Receber' ? p.realizadoAcumulado < p.previstoAcumulado : p.realizadoAcumulado > p.previstoAcumulado
      const furo: [number, number] = desfavoravel
        ? [Math.min(p.previstoAcumulado, p.realizadoAcumulado), Math.max(p.previstoAcumulado, p.realizadoAcumulado)]
        : [p.realizadoAcumulado, p.realizadoAcumulado]
      return {
        label: porDia ? diaLabel(p.data) : mesLabel(p.data),
        previstoAcumulado: p.previstoAcumulado,
        realizadoAcumulado: p.realizadoAcumulado,
        furo,
      }
    })
  }, [dados, filtroTipo, porDia])

  async function abrirItem(item: ItemPrevistoRealizado) {
    if (itemAberto === item.chave) { setItemAberto(null); return }
    setItemAberto(item.chave)
    setHistorico(null)
    setCarregandoHistorico(true)
    try {
      setHistorico(await obterHistoricoItemPrevistoRealizado(clienteId, item.chave, 6, incluirAvulsosVinculados))
    } catch (e: unknown) {
      setErro(e instanceof Error ? e.message : 'Erro ao carregar histórico do item.')
    } finally {
      setCarregandoHistorico(false)
    }
  }

  const semAnimacao = prefereMovimentoReduzido()

  function tooltipGrafico(props: { active?: boolean; payload?: readonly { payload?: (typeof dadosGrafico)[number] }[] }) {
    const { active, payload } = props
    if (!active || !payload || payload.length === 0) return null
    const p = payload[0]?.payload
    if (!p) return null
    return (
      <div className="mv-chart-tooltip">
        <div className="mv-chart-tooltip-label">{p.label}</div>
        <div className="mv-chart-tooltip-row"><span>Previsto acumulado</span><strong>{fmtBRL(p.previstoAcumulado)}</strong></div>
        <div className="mv-chart-tooltip-row"><span>Realizado acumulado</span><strong>{fmtBRL(p.realizadoAcumulado)}</strong></div>
      </div>
    )
  }

  return (
    <MvPage>
      <MvHero
        eyebrow={janela === 1 ? 'Este mês' : `Nos últimos ${janela} meses`}
        value={Math.abs(impactoNoCaixa)}
        tone={impactoNoCaixa >= 0 ? 'good' : 'bad'}
        caption={
          <>
            {janela === 1 ? 'Este mês' : `Nos últimos ${janela} meses`} você {diferencaReceber < 0
              ? <>recebeu <strong>{fmtBRL(Math.abs(diferencaReceber))}</strong> a menos</>
              : diferencaReceber > 0 ? <>recebeu <strong>{fmtBRL(diferencaReceber)}</strong> a mais</> : 'recebeu exatamente o previsto'}
            {' e '}
            {diferencaPagar > 0
              ? <>pagou <strong>{fmtBRL(diferencaPagar)}</strong> a mais</>
              : diferencaPagar < 0 ? <>pagou <strong>{fmtBRL(Math.abs(diferencaPagar))}</strong> a menos</> : 'pagou exatamente o previsto'}
            {' do que o previsto. Impacto no caixa: '}
            <strong>{impactoNoCaixa >= 0 ? '+' : '−'}{fmtBRL(Math.abs(impactoNoCaixa))}</strong>.
          </>
        }
        footnote={fmtLidoEm('contas fixas recorrentes')}
      />

      <div className="mv-controles-row">
        <MvTabs
          opcoes={[{ valor: '1', label: 'Mês atual' }, { valor: '3', label: '3 meses' }, { valor: '6', label: '6 meses' }]}
          ativa={String(janela)}
          onChange={v => setJanela(Number(v) as Janela)}
        />
        <MvTabs
          opcoes={[{ valor: 'Receber', label: '📥 Só entradas' }, { valor: 'Pagar', label: '📤 Só saídas' }]}
          ativa={filtroTipo}
          onChange={setFiltroTipo}
        />
      </div>

      <div className="mv-controles-row">
        <label className="mv-checkbox-label">
          <input
            type="checkbox"
            checked={incluirAvulsosVinculados}
            onChange={e => setIncluirAvulsosVinculados(e.target.checked)}
          />
          Incluir contas avulsas pagas por vínculo
        </label>
        <button className="btn-cancel" disabled={itensFiltrados.length === 0} onClick={() => exportarCsv(itensFiltrados)}>
          📋 Exportar CSV
        </button>
      </div>

      {loading && <p className="pj-loading">Calculando...</p>}
      {erro && <p className="pj-erro">{erro}</p>}

      {!loading && dados && itensFiltrados.length === 0 && (
        <p className="pj-vazio">Nenhuma conta fixa recorrente com título gerado no período — não há o que comparar ainda.</p>
      )}

      {!loading && itensFiltrados.length > 0 && (
        <>
          <MvSectionLabel>Previsto acumulado × realizado acumulado</MvSectionLabel>
          <div className="mv-chart-card">
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={dadosGrafico} margin={{ top: 8, right: 8, bottom: 0, left: 0 }}>
                <CartesianGrid stroke="var(--tx2)" strokeOpacity={0.12} vertical={false} />
                <XAxis dataKey="label" tick={{ fontSize: 11, fill: 'var(--tx2)' }} axisLine={{ stroke: 'var(--tx2)', strokeOpacity: 0.3 }} tickLine={false} />
                <YAxis tick={{ fontSize: 11, fill: 'var(--tx3)' }} tickFormatter={v => fmtBRLCompacto(v)} width={64} axisLine={false} tickLine={false} />
                <Tooltip content={tooltipGrafico} cursor={{ stroke: 'var(--bd)' }} />
                <Area type="monotone" dataKey="furo" stroke="none" fill="var(--danger)" fillOpacity={0.15} isAnimationActive={!semAnimacao} activeDot={false} />
                <Area
                  type="monotone" dataKey="previstoAcumulado" name="Previsto"
                  stroke="var(--tx3)" strokeWidth={2} strokeDasharray="6 4" fill="none" dot={false} isAnimationActive={!semAnimacao}
                />
                <Area
                  type="monotone" dataKey="realizadoAcumulado" name="Realizado"
                  stroke="var(--mv-blue)" strokeWidth={2} fill="none" dot={false} activeDot={{ r: 4 }} isAnimationActive={!semAnimacao}
                />
              </AreaChart>
            </ResponsiveContainer>
          </div>

          <MvKpiGrid>
            <MvKpi label="Previsto no período" value={itensFiltrados.reduce((s, i) => s + i.previsto, 0)} tone="neutral" />
            <MvKpi label="Realizado no período" value={itensFiltrados.reduce((s, i) => s + i.realizado, 0)} tone="neutral" />
          </MvKpiGrid>

          <MvSectionLabel>{`Itens previstos — ${filtroTipo === 'Receber' ? 'a receber' : 'a pagar'}`}</MvSectionLabel>
          <div className="mv-bullet-list">
            {itensFiltrados.map(item => {
              const aberto = itemAberto === item.chave
              const bom = filtroTipo === 'Receber' ? item.diferenca >= 0 : item.diferenca <= 0
              const pctTexto = item.diferencaPercentual != null ? ` (${fmtVariacao(item.diferencaPercentual)})` : ''
              return (
                <button key={item.chave} type="button" className="mv-bullet-row" onClick={() => abrirItem(item)}>
                  <div className="mv-bullet-header">
                    <span className="mv-bullet-categoria">
                      {item.nome}
                      {item.categoria && <span className="mv-chip" style={{ marginLeft: 8 }}>{item.categoria}</span>}
                    </span>
                    <span className={`mv-tab mv-tag-status mv-tone-${STATUS_TONE[item.status]}`} style={{ cursor: 'default' }}>
                      {STATUS_LABEL[item.status]}
                    </span>
                  </div>
                  <div className="mv-bullet-expandida-grid" style={{ marginTop: 2 }}>
                    <div><span>Previsto</span><strong>{fmtBRL(item.previsto)}</strong></div>
                    <div><span>Realizado</span><strong>{fmtBRL(item.realizado)}</strong></div>
                    <div>
                      <span>Diferença</span>
                      <strong style={{ color: item.diferenca === 0 ? 'var(--tx1)' : bom ? 'var(--success)' : 'var(--danger)' }}>
                        {item.diferenca >= 0 ? '+' : ''}{fmtBRL(item.diferenca)}{pctTexto}
                      </strong>
                    </div>
                  </div>

                  {aberto && (
                    <div className="mv-bullet-expandida">
                      {carregandoHistorico && <p style={{ color: 'var(--tx3)', fontSize: 12 }}>Carregando histórico...</p>}
                      {!carregandoHistorico && historico && historico.meses.length > 0 && (
                        <div style={{ height: 140 }}>
                          <ResponsiveContainer width="100%" height="100%">
                            <LineChart data={historico.meses.map(p => ({ mes: mesLabel(p.mes), previsto: p.previsto, realizado: p.realizado }))}>
                              <XAxis dataKey="mes" stroke="var(--tx3)" tick={{ fontSize: 10, fill: 'var(--tx3)' }} axisLine={false} tickLine={false} />
                              <YAxis hide domain={['auto', 'auto']} />
                              <Tooltip
                                formatter={(v, name) => [typeof v === 'number' ? fmtBRL(v) : String(v ?? ''), name === 'previsto' ? 'Previsto' : 'Realizado']}
                                contentStyle={{ background: 'var(--bg-card)', border: '1px solid var(--bd)', fontSize: 12, borderRadius: 10 }}
                                labelStyle={{ color: 'var(--tx2)' }}
                              />
                              <Line type="monotone" dataKey="previsto" stroke="var(--tx3)" strokeWidth={2} strokeDasharray="4 3" dot={false} isAnimationActive={!semAnimacao} />
                              <Line type="monotone" dataKey="realizado" stroke="var(--mv-blue)" strokeWidth={2} dot={{ r: 2.5 }} isAnimationActive={!semAnimacao} />
                            </LineChart>
                          </ResponsiveContainer>
                        </div>
                      )}
                      {!carregandoHistorico && historico && historico.meses.length === 0 && (
                        <p style={{ color: 'var(--tx3)', fontSize: 12 }}>Sem histórico suficiente pra esse favorecido ainda.</p>
                      )}
                    </div>
                  )}
                </button>
              )
            })}
          </div>

          <MvDetails label="Ver tabela completa">
            <div style={{ overflowX: 'auto' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
                <thead>
                  <tr style={{ textAlign: 'left', borderBottom: '1px solid var(--bd)' }}>
                    <th style={{ padding: '8px 10px' }}>Nome</th>
                    <th style={{ padding: '8px 10px' }}>Categoria</th>
                    <th style={{ padding: '8px 10px', textAlign: 'right' }}>Previsto</th>
                    <th style={{ padding: '8px 10px', textAlign: 'right' }}>Realizado</th>
                    <th style={{ padding: '8px 10px', textAlign: 'right' }}>Diferença</th>
                    <th style={{ padding: '8px 10px' }}>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {itensFiltrados.map(item => (
                    <tr key={item.chave} style={{ borderBottom: '1px solid var(--bd)' }}>
                      <td style={{ padding: '6px 10px' }}>{item.nome}</td>
                      <td style={{ padding: '6px 10px' }}>{item.categoria ?? '—'}</td>
                      <td style={{ padding: '6px 10px', textAlign: 'right' }}>{fmtBRL(item.previsto)}</td>
                      <td style={{ padding: '6px 10px', textAlign: 'right' }}>{fmtBRL(item.realizado)}</td>
                      <td style={{ padding: '6px 10px', textAlign: 'right', color: item.diferenca === 0 ? 'inherit' : 'var(--danger)', fontWeight: item.diferenca !== 0 ? 600 : 400 }}>
                        {item.diferenca >= 0 ? '+' : ''}{fmtBRL(item.diferenca)}
                        {item.diferencaPercentual != null && ` (${fmtVariacao(item.diferencaPercentual)})`}
                      </td>
                      <td style={{ padding: '6px 10px' }}>{STATUS_LABEL[item.status]}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </MvDetails>
        </>
      )}
    </MvPage>
  )
}
