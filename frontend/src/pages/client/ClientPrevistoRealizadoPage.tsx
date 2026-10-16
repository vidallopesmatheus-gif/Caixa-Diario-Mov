import { useState, useEffect, useCallback, useMemo } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { obterPrevistoRealizado } from '../../api/relatorios'
import type { PrevistoRealizado, LinhaPrevistoRealizado } from '../../api/relatorios'
import { fmtBRL, fmtBRLCompacto, fmtLidoEm } from '../../utils/format'
import { MvPage, MvHero, MvSectionLabel, MvKpi, MvKpiGrid, MvTabs, MvInsight, MvDetails, prefereMovimentoReduzido } from '../../components/mv'
import { LineChart, Line, XAxis, YAxis, Tooltip, ResponsiveContainer } from 'recharts'
import './ClientProjecao.css'

const MESES_ABREV = ['Jan','Fev','Mar','Abr','Mai','Jun','Jul','Ago','Set','Out','Nov','Dez']

function mesLabel(mes: string): string {
  const [ano, m] = mes.split('-')
  return `${MESES_ABREV[Number(m) - 1]}/${ano.slice(2)}`
}

function fmtVariacao(fracao: number): string {
  const sinal = fracao >= 0 ? '+' : ''
  return `${sinal}${(fracao * 100).toLocaleString('pt-BR', { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%`
}

function fmtPrecisao(n: number): string {
  return `${n.toLocaleString('pt-BR', { maximumFractionDigits: 1 })}%`
}

function csvEscape(valor: string): string {
  return /[",;\n]/.test(valor) ? `"${valor.replace(/"/g, '""')}"` : valor
}

function exportarCsv(linhas: LinhaPrevistoRealizado[]) {
  const linhasCsv = [['Tipo', 'Categoria', 'Mês', 'Previsto', 'Realizado', 'Variação %', 'Variação alta', 'Subiu 3 meses seguidos']]
  for (const linha of linhas) {
    for (const ponto of linha.meses) {
      linhasCsv.push([
        linha.tipo, linha.categoria, ponto.mes,
        ponto.previsto.toFixed(2), ponto.realizado.toFixed(2),
        ponto.variacaoPercentual != null ? (ponto.variacaoPercentual * 100).toFixed(1) : '',
        ponto.variacaoAlta ? 'Sim' : 'Não',
        ponto.subiuTresMesesSeguidos ? 'Sim' : 'Não',
      ])
    }
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

interface LinhaBullet {
  linha: LinhaPrevistoRealizado
  previsto: number
  realizado: number
  bandaMin: number
  bandaMax: number
  diferenca: number
  variacaoPercentual?: number
  bom: boolean
  alerta: boolean
}

export default function ClientPrevistoRealizadoPage() {
  const { user } = useAuth()
  const clienteId = user?.usuarioId ?? ''

  const [meses, setMeses] = useState<3 | 6 | 12>(6)
  const [filtroTipo, setFiltroTipo] = useState<'Pagar' | 'Receber'>('Pagar')
  const [incluirAvulsosVinculados, setIncluirAvulsosVinculados] = useState(false)
  const [dados, setDados] = useState<PrevistoRealizado | null>(null)
  const [loading, setLoading] = useState(false)
  const [erro, setErro] = useState('')
  const [categoriaExpandida, setCategoriaExpandida] = useState<string | null>(null)

  const carregar = useCallback(async () => {
    if (!clienteId) return
    setLoading(true); setErro('')
    try {
      setDados(await obterPrevistoRealizado(clienteId, meses, incluirAvulsosVinculados))
    } catch (e: unknown) {
      setErro(e instanceof Error ? e.message : 'Erro ao carregar relatório.')
    } finally {
      setLoading(false)
    }
  }, [clienteId, meses, incluirAvulsosVinculados])

  useEffect(() => { carregar() }, [carregar])

  const linhasFiltradas = useMemo(
    () => dados?.linhas.filter(l => l.tipo === filtroTipo) ?? [],
    [dados, filtroTipo],
  )

  // Linha de "bullet chart": último mês do período pra previsto/realizado (mesma escala, comparável
  // ponto a ponto), banda de variação normal = min-max do realizado nos últimos 3 meses.
  const bullets = useMemo<LinhaBullet[]>(() => linhasFiltradas.map(linha => {
    const ultimo = linha.meses.at(-1)
    const ultimos3 = linha.meses.slice(-3).map(p => p.realizado)
    const previsto = ultimo?.previsto ?? 0
    const realizado = ultimo?.realizado ?? 0
    const diferenca = realizado - previsto
    const bom = filtroTipo === 'Pagar' ? diferenca <= 0 : diferenca >= 0
    return {
      linha,
      previsto,
      realizado,
      bandaMin: ultimos3.length > 0 ? Math.min(...ultimos3) : realizado,
      bandaMax: ultimos3.length > 0 ? Math.max(...ultimos3) : realizado,
      diferenca,
      variacaoPercentual: ultimo?.variacaoPercentual,
      bom,
      alerta: ultimo?.variacaoAlta ?? false,
    }
  }), [linhasFiltradas, filtroTipo])

  const maxDominio = useMemo(() => {
    const valores = bullets.flatMap(b => [b.previsto, b.realizado, b.bandaMin, b.bandaMax])
    return Math.max(1, ...valores) * 1.15
  }, [bullets])

  // Agregado do período inteiro (todos os meses da janela 3/6/12, não só o último) — é o "você
  // gastou/recebeu X a mais/menos do que o previsto" no total do período selecionado.
  const somaPrevisto = useMemo(() => linhasFiltradas.reduce((s, l) => s + l.meses.reduce((s2, p) => s2 + p.previsto, 0), 0), [linhasFiltradas])
  const somaRealizado = useMemo(() => linhasFiltradas.reduce((s, l) => s + l.meses.reduce((s2, p) => s2 + p.realizado, 0), 0), [linhasFiltradas])
  const diferencaTotal = somaRealizado - somaPrevisto
  const precisao = somaPrevisto > 0 ? Math.max(0, 1 - Math.abs(diferencaTotal) / somaPrevisto) * 100 : 100

  const maiorDesvio = useMemo(() => {
    if (linhasFiltradas.length === 0) return undefined
    return linhasFiltradas
      .map(l => ({ categoria: l.categoria, desvio: l.meses.reduce((s, p) => s + (p.realizado - p.previsto), 0) }))
      .reduce((pior, atual) => Math.abs(atual.desvio) > Math.abs(pior.desvio) ? atual : pior)
  }, [linhasFiltradas])

  const categoriasAlerta = useMemo(
    () => linhasFiltradas.filter(l => l.meses.some(p => p.variacaoAlta)).length,
    [linhasFiltradas],
  )

  const bomTotal = filtroTipo === 'Pagar' ? diferencaTotal <= 0 : diferencaTotal >= 0
  const verbo = filtroTipo === 'Pagar' ? 'gastou' : 'recebeu'

  function toggleExpandida(chave: string) {
    setCategoriaExpandida(prev => prev === chave ? null : chave)
  }

  const semAnimacao = prefereMovimentoReduzido()

  return (
    <MvPage>
      <MvHero
        eyebrow={filtroTipo === 'Pagar' ? 'Suas contas a pagar' : 'Suas contas a receber'}
        value={Math.abs(diferencaTotal)}
        tone={bomTotal ? 'good' : 'bad'}
        contraste={{ valor: precisao, formatter: fmtPrecisao }}
        caption={
          diferencaTotal === 0
            ? <>Você {verbo} exatamente o previsto no período selecionado.</>
            : <>Você {verbo} <strong>{fmtBRL(Math.abs(diferencaTotal))}</strong> {diferencaTotal > 0 ? 'a mais' : 'a menos'} do que o previsto — uma precisão de <strong>{fmtPrecisao(precisao)}</strong> no período.</>
        }
        footnote={fmtLidoEm(`contas fixas recorrentes · ${meses} meses`)}
      />

      <div className="mv-controles-row">
        <MvTabs
          opcoes={[{ valor: 'Pagar', label: '📤 Pagar' }, { valor: 'Receber', label: '📥 Receber' }]}
          ativa={filtroTipo}
          onChange={setFiltroTipo}
        />
        <MvTabs
          opcoes={[{ valor: '3', label: '3 meses' }, { valor: '6', label: '6 meses' }, { valor: '12', label: '12 meses' }]}
          ativa={String(meses)}
          onChange={v => setMeses(Number(v) as 3 | 6 | 12)}
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
        <button className="btn-cancel" disabled={linhasFiltradas.length === 0} onClick={() => exportarCsv(linhasFiltradas)}>
          📋 Exportar CSV
        </button>
      </div>

      {loading && <p className="pj-loading">Calculando...</p>}
      {erro && <p className="pj-erro">{erro}</p>}

      {!loading && dados && linhasFiltradas.length === 0 && (
        <p className="pj-vazio">Nenhuma conta fixa recorrente com título gerado no período — não há o que comparar ainda.</p>
      )}

      {!loading && linhasFiltradas.length > 0 && (
        <>
          <MvKpiGrid>
            <MvKpi label="Previsto no período" value={somaPrevisto} tone="neutral" />
            <MvKpi label="Realizado no período" value={somaRealizado} tone="neutral" />
            <MvKpi
              label="Maior desvio"
              value={maiorDesvio ? Math.abs(maiorDesvio.desvio) : 0}
              delta={maiorDesvio?.categoria}
              tone={maiorDesvio && maiorDesvio.desvio !== 0 ? (filtroTipo === 'Pagar' ? (maiorDesvio.desvio > 0 ? 'bad' : 'good') : (maiorDesvio.desvio < 0 ? 'bad' : 'good')) : 'good'}
            />
            <MvKpi
              label="Categorias com alerta"
              value={categoriasAlerta}
              formatter={n => `${n} categoria${n === 1 ? '' : 's'}`}
              tone={categoriasAlerta > 0 ? 'bad' : 'good'}
            />
          </MvKpiGrid>

          {categoriasAlerta > 0 && (
            <MvInsight tone="warn" icon="⚠️" text={<><strong>{categoriasAlerta}</strong> categoria{categoriasAlerta === 1 ? '' : 's'} subiu 3 meses seguidos ou teve variação alta — veja o selo nas linhas abaixo.</>} />
          )}

          <MvSectionLabel>Previsto × realizado por categoria — último mês do período</MvSectionLabel>
          <div className="mv-bullet-list">
            {bullets.map(b => {
              const chave = `${b.linha.tipo}|${b.linha.categoria}`
              const expandida = categoriaExpandida === chave
              const pctTexto = b.variacaoPercentual != null ? ` (${fmtVariacao(b.variacaoPercentual)})` : ''
              return (
                <button key={chave} type="button" className="mv-bullet-row" onClick={() => toggleExpandida(chave)}>
                  <div className="mv-bullet-header">
                    <span className="mv-bullet-categoria">
                      {b.linha.categoria}
                      {b.alerta && <span title="Subiu 3 meses seguidos / variação alta"> ⚠️</span>}
                    </span>
                    <span className="mv-bullet-delta" style={{ color: b.bom ? 'var(--success)' : 'var(--danger)' }}>
                      {b.diferenca >= 0 ? '+' : ''}{fmtBRL(b.diferenca)}{pctTexto}
                    </span>
                  </div>
                  <div className="mv-bullet-track">
                    <div className="mv-bullet-band" style={{
                      left: `${(b.bandaMin / maxDominio) * 100}%`,
                      width: `${Math.max(0, ((b.bandaMax - b.bandaMin) / maxDominio) * 100)}%`,
                    }} />
                    <div className="mv-bullet-bar" style={{
                      width: `${Math.max(0.5, (b.realizado / maxDominio) * 100)}%`,
                      background: b.bom ? 'var(--success)' : 'var(--danger)',
                    }} />
                    <div className="mv-bullet-marker" style={{ left: `${(b.previsto / maxDominio) * 100}%` }} />
                  </div>

                  {expandida && (
                    <div className="mv-bullet-expandida">
                      <div className="mv-bullet-expandida-grid">
                        <div><span>Previsto</span><strong>{fmtBRL(b.previsto)}</strong></div>
                        <div><span>Realizado</span><strong>{fmtBRL(b.realizado)}</strong></div>
                        <div><span>Faixa normal (3m)</span><strong>{fmtBRLCompacto(b.bandaMin)} – {fmtBRLCompacto(b.bandaMax)}</strong></div>
                      </div>
                      <div style={{ height: 120 }}>
                        <ResponsiveContainer width="100%" height="100%">
                          <LineChart data={b.linha.meses.slice(-6).map(p => ({ mes: mesLabel(p.mes), previsto: p.previsto, realizado: p.realizado }))}>
                            <XAxis dataKey="mes" stroke="var(--tx3)" tick={{ fontSize: 10, fill: 'var(--tx3)' }} axisLine={false} tickLine={false} />
                            <YAxis hide domain={['auto', 'auto']} />
                            <Tooltip
                              formatter={(v, name) => [typeof v === 'number' ? fmtBRL(v) : String(v ?? ''), name === 'previsto' ? 'Previsto' : 'Realizado']}
                              contentStyle={{ background: 'var(--bg-card)', border: '1px solid var(--bd)', fontSize: 12, borderRadius: 10 }}
                              labelStyle={{ color: 'var(--tx2)' }}
                            />
                            <Line type="monotone" dataKey="previsto" stroke="var(--tx3)" strokeWidth={2} strokeDasharray="4 3" dot={false} isAnimationActive={!semAnimacao} />
                            <Line type="monotone" dataKey="realizado" stroke="var(--mv-blue-light)" strokeWidth={2} dot={{ r: 2.5 }} isAnimationActive={!semAnimacao} />
                          </LineChart>
                        </ResponsiveContainer>
                      </div>
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
                    <th style={{ padding: '8px 10px' }}>Categoria</th>
                    <th style={{ padding: '8px 10px' }}>Mês</th>
                    <th style={{ padding: '8px 10px', textAlign: 'right' }}>Previsto</th>
                    <th style={{ padding: '8px 10px', textAlign: 'right' }}>Realizado</th>
                    <th style={{ padding: '8px 10px', textAlign: 'right' }}>Variação</th>
                  </tr>
                </thead>
                <tbody>
                  {linhasFiltradas.flatMap(linha => linha.meses.map(ponto => (
                    <tr key={`${linha.tipo}-${linha.categoria}-${ponto.mes}`} style={{ borderBottom: '1px solid var(--bd)' }}>
                      <td style={{ padding: '6px 10px' }}>{linha.categoria}</td>
                      <td style={{ padding: '6px 10px' }}>{mesLabel(ponto.mes)}</td>
                      <td style={{ padding: '6px 10px', textAlign: 'right' }}>{fmtBRL(ponto.previsto)}</td>
                      <td style={{ padding: '6px 10px', textAlign: 'right' }}>{fmtBRL(ponto.realizado)}</td>
                      <td style={{ padding: '6px 10px', textAlign: 'right', color: ponto.variacaoAlta ? 'var(--danger)' : 'inherit', fontWeight: ponto.variacaoAlta ? 600 : 400 }}>
                        {ponto.variacaoPercentual != null ? fmtVariacao(ponto.variacaoPercentual) : '—'}
                        {ponto.subiuTresMesesSeguidos && <span title="Subiu 3 meses seguidos"> ⚠️↑</span>}
                      </td>
                    </tr>
                  )))}
                </tbody>
              </table>
            </div>
          </MvDetails>
        </>
      )}
    </MvPage>
  )
}
