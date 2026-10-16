import { useState, useEffect, useCallback, useMemo } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { obterMeuFuturo } from '../../api/projecao'
import { obterPreferenciasFuturo, atualizarPreferenciasFuturo } from '../../api/preferenciasFuturo'
import { fmtBRL, fmtBRLCompacto, fmtLidoEm, monthLabel } from '../../utils/format'
import { MvPage, MvHero, MvSectionLabel, MvKpi, MvKpiGrid, MvInsight, MvDetails, prefereMovimentoReduzido } from '../../components/mv'
import type { MeuFuturo, MeuFuturoMes } from '../../api/projecao'
import type { PreferenciasFuturo } from '../../api/preferenciasFuturo'
import {
  AreaChart, Area, CartesianGrid, XAxis, YAxis, Tooltip, ResponsiveContainer, ReferenceLine, ReferenceArea,
} from 'recharts'
import './ClientMeuFuturo.css'

function fmtNum(n: number) {
  return n.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}
function parseBRL(s: string): number {
  const n = parseFloat(s.replace(/\./g, '').replace(',', '.'))
  return Number.isFinite(n) ? n : 0
}

const STATUS_LABEL: Record<string, string> = { Tranquilo: '🟢 Tranquilo', Atencao: '🟡 Atenção', Risco: '🔴 Risco' }
const STATUS_CLASSE: Record<string, string> = { Tranquilo: 'mf-status-tranquilo', Atencao: 'mf-status-atencao', Risco: 'mf-status-risco' }
const STATUS_ICONE: Record<string, string> = { Tranquilo: '🟢', Atencao: '🟡', Risco: '🔴' }
const STATUS_TONE: Record<string, 'good' | 'warn' | 'bad'> = { Tranquilo: 'good', Atencao: 'warn', Risco: 'bad' }

export default function ClientMeuFuturoPage() {
  const { user } = useAuth()
  const clienteId = user?.usuarioId ?? ''

  const [preferencias, setPreferencias] = useState<PreferenciasFuturo>({})
  const [reservaDisplay, setReservaDisplay] = useState('')
  const [salvandoPreferencias, setSalvandoPreferencias] = useState(false)
  const [msgPreferencias, setMsgPreferencias] = useState('')

  // Simulador "E se...?" — item 3 do plano, pós-processamento sobre a tabela já calculada.
  const [ajusteReceitaPercent, setAjusteReceitaPercent] = useState(0)
  const [atrasoRecebimentosDias, setAtrasoRecebimentosDias] = useState(0)

  const [meuFuturo, setMeuFuturo] = useState<MeuFuturo | null>(null)
  const [loading, setLoading] = useState(true)
  const [erro, setErro] = useState('')

  // Redesign visual (Money Vision): mês selecionado na tira de pills, pra abrir o card de detalhe.
  const [mesSelecionado, setMesSelecionado] = useState<string | null>(null)

  useEffect(() => {
    if (!clienteId) return
    obterPreferenciasFuturo(clienteId).then(p => {
      setPreferencias(p)
      setReservaDisplay(p.reservaMinima ? fmtNum(p.reservaMinima) : '')
    }).catch(() => {})
  }, [clienteId])

  const carregar = useCallback(async () => {
    if (!clienteId) return
    setLoading(true); setErro('')
    try {
      const data = await obterMeuFuturo(clienteId, {
        reservaMinima: preferencias.reservaMinima,
        ajusteReceitaPercent: ajusteReceitaPercent || undefined,
        atrasoRecebimentosDias: atrasoRecebimentosDias || undefined,
      })
      setMeuFuturo(data)
    } catch (e: unknown) {
      setErro(e instanceof Error ? e.message : 'Erro ao carregar "Meu Futuro".')
    } finally {
      setLoading(false)
    }
  }, [clienteId, preferencias.reservaMinima, ajusteReceitaPercent, atrasoRecebimentosDias])

  useEffect(() => { carregar() }, [carregar])

  async function salvarPreferencias(tipoPessoa?: 'PF' | 'PJ') {
    if (!clienteId) return
    setSalvandoPreferencias(true); setMsgPreferencias('')
    try {
      const dto = { reservaMinima: parseBRL(reservaDisplay) || undefined, tipoPessoa: tipoPessoa ?? preferencias.tipoPessoa }
      const atualizado = await atualizarPreferenciasFuturo(clienteId, dto)
      setPreferencias(atualizado)
      setMsgPreferencias('Preferências salvas!')
    } catch (e: unknown) {
      setMsgPreferencias(e instanceof Error ? e.message : 'Erro ao salvar preferências.')
    } finally {
      setSalvandoPreferencias(false)
    }
  }

  const piorMes = useMemo(() => {
    if (!meuFuturo || meuFuturo.meses.length === 0) return undefined
    return meuFuturo.meses.reduce((pior, m) => m.saldoFinal < pior.saldoFinal ? m : pior, meuFuturo.meses[0])
  }, [meuFuturo])

  const mesAnterior = useMemo(() => {
    if (!meuFuturo || !piorMes) return undefined
    const idx = meuFuturo.meses.findIndex(m => m.mes === piorMes.mes)
    return idx > 0 ? meuFuturo.meses[idx - 1] : undefined
  }, [meuFuturo, piorMes])

  const maiorNecessidade = useMemo(() => {
    if (!meuFuturo) return undefined
    return meuFuturo.meses.reduce((maior, m) => m.precisaReceber > maior.precisaReceber ? m : maior, meuFuturo.meses[0])
  }, [meuFuturo])

  const qtdPorStatus = useMemo(() => {
    const c = { Tranquilo: 0, Atencao: 0, Risco: 0 }
    meuFuturo?.meses.forEach(m => { c[m.status]++ })
    return c
  }, [meuFuturo])

  const dadosGrafico = useMemo(() => meuFuturo?.meses.map(m => ({
    mes: monthLabel(m.mes),
    aReceber: m.aReceber,
    aPagar: -m.aPagar,
    saldoFinal: m.saldoFinal,
    precisaReceber: m.precisaReceber,
    status: m.status,
    mesRaw: m.mes,
  })) ?? [], [meuFuturo])

  function tooltipGrafico(props: { active?: boolean; payload?: readonly { payload?: (typeof dadosGrafico)[number] }[] }) {
    const { active, payload } = props
    if (!active || !payload || payload.length === 0) return null
    const p = payload[0]?.payload
    if (!p) return null
    return (
      <div className="mv-chart-tooltip">
        <div className="mv-chart-tooltip-label">{p.mes} · {STATUS_LABEL[p.status]}</div>
        <div className="mv-chart-tooltip-row"><span style={{ color: 'var(--success)' }}>A receber</span><strong>+{fmtBRL(p.aReceber)}</strong></div>
        <div className="mv-chart-tooltip-row"><span style={{ color: 'var(--danger)' }}>A pagar</span><strong>-{fmtBRL(Math.abs(p.aPagar))}</strong></div>
        <div className="mv-chart-tooltip-row"><span>Saldo final</span><strong>{fmtBRL(p.saldoFinal)}</strong></div>
        {p.precisaReceber > 0 && (
          <div className="mv-chart-tooltip-row"><span style={{ color: 'var(--warning)' }}>Precisa receber</span><strong>{fmtBRL(p.precisaReceber)}</strong></div>
        )}
      </div>
    )
  }

  function pontoLinha(props: { cx?: number; cy?: number; payload?: (typeof dadosGrafico)[number] }) {
    const { cx, cy, payload } = props
    if (cx === undefined || cy === undefined) return <circle cx={0} cy={0} r={0} />
    if (payload?.status !== 'Risco') return <circle cx={cx} cy={cy} r={2.5} fill="var(--mv-blue)" />
    return (
      <g>
        <circle cx={cx} cy={cy} r={9} fill="var(--danger)" opacity={0.18} />
        <circle cx={cx} cy={cy} r={4.5} fill="var(--danger)" stroke="var(--bg)" strokeWidth={2} />
        <text x={cx} y={cy - 14} textAnchor="middle" fontSize={10.5} fontWeight={700} fill="var(--danger)">
          precisa de {fmtBRLCompacto(payload.precisaReceber)}
        </text>
      </g>
    )
  }

  const mesDetalhe = meuFuturo?.meses.find(m => m.mes === mesSelecionado)
  const semAnimacao = prefereMovimentoReduzido()
  const reservaMinima = preferencias.reservaMinima ?? null

  const dominioY = useMemo(() => {
    if (dadosGrafico.length === 0) return undefined
    const valores = dadosGrafico.map(p => p.saldoFinal)
    if (reservaMinima !== null) valores.push(reservaMinima)
    const min = Math.min(...valores)
    const max = Math.max(...valores)
    const margem = Math.max((max - min) * 0.1, 1)
    return [min - margem, max + margem] as [number, number]
  }, [dadosGrafico, reservaMinima])

  return (
    <MvPage>
      {piorMes && (
        <MvHero
          eyebrow={`Pior momento projetado · ${monthLabel(piorMes.mes)}`}
          value={piorMes.saldoFinal}
          tone={piorMes.saldoFinal < 0 ? 'bad' : STATUS_TONE[piorMes.status]}
          contraste={meuFuturo ? { valor: meuFuturo.meses[0].saldoInicial } : undefined}
          caption={
            piorMes.status === 'Tranquilo'
              ? <>Seu dinheiro cobre todas as contas nos próximos 12 meses — nenhum mês fica apertado.</>
              : piorMes.saldoFinal < 0
                ? <>Seu dinheiro cobre suas contas {mesAnterior ? <>até <strong>{monthLabel(mesAnterior.mes)}</strong></> : 'só até o mês atual'}. Em <strong>{monthLabel(piorMes.mes)}</strong>, faltam <strong>{fmtBRL(piorMes.precisaReceber || Math.abs(piorMes.saldoFinal))}</strong>.</>
                : <>Em <strong>{monthLabel(piorMes.mes)}</strong> o mês fica apertado — sobra pouco, mas não falta dinheiro.</>
          }
          footnote={fmtLidoEm('projeção de 12 meses')}
        />
      )}

      <div className="mf-preferencias">
        <label className="mf-pref-item">
          Reserva mínima
          <div className="val-input-wrap">
            <span className="val-prefix">R$</span>
            <input
              type="text" inputMode="decimal" placeholder="0,00"
              value={reservaDisplay}
              onChange={e => setReservaDisplay(e.target.value.replace(/[^\d,]/g, ''))}
              onBlur={() => salvarPreferencias()}
            />
          </div>
        </label>
        <div className="mf-pref-item">
          Eu sou
          <div className="mf-pf-pj-toggle">
            <button
              className={preferencias.tipoPessoa !== 'PF' ? 'active' : ''}
              disabled={salvandoPreferencias}
              onClick={() => salvarPreferencias('PJ')}
            >
              Pessoa Jurídica
            </button>
            <button
              className={preferencias.tipoPessoa === 'PF' ? 'active' : ''}
              disabled={salvandoPreferencias}
              onClick={() => salvarPreferencias('PF')}
            >
              Pessoa Física
            </button>
          </div>
        </div>
        {msgPreferencias && <span className="mf-pref-msg">{msgPreferencias}</span>}
      </div>

      {loading && <p className="mf-loading">Calculando...</p>}
      {erro && <p className="mf-erro">{erro}</p>}

      {meuFuturo && !loading && (
        <>
          <MvKpiGrid>
            <MvKpi label="Meses tranquilos" value={qtdPorStatus.Tranquilo} formatter={n => `${n} mês${n === 1 ? '' : 'es'}`} tone="good" />
            <MvKpi label="Meses em risco" value={qtdPorStatus.Risco} formatter={n => `${n} mês${n === 1 ? '' : 'es'}`} tone={qtdPorStatus.Risco > 0 ? 'bad' : 'good'} />
            <MvKpi
              label="Maior necessidade"
              value={maiorNecessidade?.precisaReceber ?? 0}
              tone={maiorNecessidade && maiorNecessidade.precisaReceber > 0 ? 'bad' : 'good'}
              delta={maiorNecessidade && maiorNecessidade.precisaReceber > 0 ? monthLabel(maiorNecessidade.mes) : undefined}
            />
            <MvKpi
              label="Saldo em 12 meses"
              value={meuFuturo.meses.at(-1)?.saldoFinal ?? 0}
              tone={(meuFuturo.meses.at(-1)?.saldoFinal ?? 0) >= 0 ? 'good' : 'bad'}
            />
          </MvKpiGrid>

          {meuFuturo.conselhos.slice(0, 3).map((c, i) => (
            <MvInsight key={i} icon="💬" text={c} tone="neutral" />
          ))}

          {/* Redesign (item 6, rodada de ajustes): gráfico principal só com o saldo acumulado —
              entradas/saídas do mês saem daqui e vão pro card de detalhe da pill (abaixo). No
              máximo 2 cores: azul de marca pro saldo, vermelho só nos meses de risco. */}
          <MvSectionLabel>Saldo acumulado — 12 meses</MvSectionLabel>
          <div className="mv-chart-card">
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={dadosGrafico} margin={{ top: 24, right: 8, bottom: 0, left: 0 }}>
                <defs>
                  <linearGradient id="gradSaldoMf" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%"  stopColor="var(--mv-blue)" stopOpacity={0.28} />
                    <stop offset="95%" stopColor="var(--mv-blue)" stopOpacity={0.02} />
                  </linearGradient>
                </defs>
                <CartesianGrid stroke="var(--tx2)" strokeOpacity={0.12} vertical={false} />
                <XAxis dataKey="mes" tick={{ fontSize: 11, fill: 'var(--tx2)' }} axisLine={{ stroke: 'var(--tx2)', strokeOpacity: 0.3 }} tickLine={false} />
                <YAxis tick={{ fontSize: 11, fill: 'var(--tx3)' }} tickFormatter={v => fmtBRLCompacto(v)} width={64} axisLine={false} tickLine={false} domain={dominioY} />
                <Tooltip content={tooltipGrafico} cursor={{ stroke: 'var(--bd)' }} />
                {reservaMinima !== null && dominioY && (
                  <>
                    <ReferenceArea y1={dominioY[0]} y2={reservaMinima} fill="var(--danger)" fillOpacity={0.08} ifOverflow="extendDomain" />
                    <ReferenceLine y={reservaMinima} stroke="var(--danger)" strokeDasharray="4 2" strokeOpacity={0.6}
                      label={{ value: 'Reserva mínima', fill: 'var(--danger)', fontSize: 10.5, position: 'insideTopLeft' }} />
                  </>
                )}
                <Area
                  type="monotone" dataKey="saldoFinal" name="Saldo"
                  stroke="var(--mv-blue)" strokeWidth={2} strokeDasharray="6 4"
                  fill="url(#gradSaldoMf)" dot={pontoLinha} activeDot={{ r: 5 }} isAnimationActive={!semAnimacao}
                />
              </AreaChart>
            </ResponsiveContainer>
          </div>

          <MvSectionLabel>Mês a mês</MvSectionLabel>
          <div className="mv-pills-strip">
            {meuFuturo.meses.map(m => (
              <button
                key={m.mes}
                type="button"
                className={`mv-pill-mes mv-tone-${STATUS_TONE[m.status]}${mesSelecionado === m.mes ? ' mv-pill-selecionada' : ''}`}
                onClick={() => setMesSelecionado(prev => prev === m.mes ? null : m.mes)}
              >
                <span className="mv-pill-status">{STATUS_ICONE[m.status]}</span>
                <span className="mv-pill-mes-label">{monthLabel(m.mes)}</span>
                <span className="mv-pill-mes-valor">{fmtBRLCompacto(m.saldoFinal)}</span>
              </button>
            ))}
          </div>

          {mesDetalhe && (
            <div className="mv-mes-detalhe">
              <div className="mv-mes-detalhe-titulo">{STATUS_ICONE[mesDetalhe.status]} {monthLabel(mesDetalhe.mes)} — {STATUS_LABEL[mesDetalhe.status].replace(/^\S+\s/, '')}</div>
              <div className="mv-mes-detalhe-grid">
                <div className="mv-mes-detalhe-item">
                  <span className="mv-mes-detalhe-label">Recebe</span>
                  <span className="mv-mes-detalhe-valor" style={{ color: 'var(--success)' }}>+{fmtBRL(mesDetalhe.aReceber)}</span>
                </div>
                <div className="mv-mes-detalhe-item">
                  <span className="mv-mes-detalhe-label">Paga</span>
                  <span className="mv-mes-detalhe-valor" style={{ color: 'var(--danger)' }}>-{fmtBRL(mesDetalhe.aPagar)}</span>
                </div>
                <div className="mv-mes-detalhe-item">
                  <span className="mv-mes-detalhe-label">Sobra no mês</span>
                  <span className="mv-mes-detalhe-valor" style={{ color: (mesDetalhe.aReceber - mesDetalhe.aPagar) >= 0 ? 'var(--success)' : 'var(--danger)' }}>
                    {fmtBRL(mesDetalhe.aReceber - mesDetalhe.aPagar)}
                  </span>
                </div>
                <div className="mv-mes-detalhe-item">
                  <span className="mv-mes-detalhe-label">Precisa receber</span>
                  <span className="mv-mes-detalhe-valor" style={{ color: mesDetalhe.precisaReceber > 0 ? 'var(--warning)' : 'var(--tx2)' }}>
                    {mesDetalhe.precisaReceber > 0 ? fmtBRL(mesDetalhe.precisaReceber) : '—'}
                  </span>
                </div>
              </div>
            </div>
          )}

          <div className="mf-simulador">
            <h3 className="mf-simulador-titulo">🎛️ E se...?</h3>
            <div className="mf-simulador-controles">
              <label>
                Receita variar em <strong>{ajusteReceitaPercent > 0 ? '+' : ''}{ajusteReceitaPercent}%</strong>
                <input
                  type="range" min={-30} max={30} step={5}
                  value={ajusteReceitaPercent}
                  onChange={e => setAjusteReceitaPercent(Number(e.target.value))}
                />
              </label>
              <label>
                Atraso nos recebimentos: <strong>{atrasoRecebimentosDias} dia(s)</strong>
                <input
                  type="range" min={0} max={60} step={5}
                  value={atrasoRecebimentosDias}
                  onChange={e => setAtrasoRecebimentosDias(Number(e.target.value))}
                />
              </label>
              {(ajusteReceitaPercent !== 0 || atrasoRecebimentosDias !== 0) && (
                <button className="btn-cancel" onClick={() => { setAjusteReceitaPercent(0); setAtrasoRecebimentosDias(0) }}>
                  Limpar simulação
                </button>
              )}
            </div>
          </div>

          <MvDetails label="Ver tabela completa">
            <div className="mf-tabela-container">
              <table className="mf-tabela">
                <thead>
                  <tr>
                    <th>Mês</th>
                    <th>Saldo inicial</th>
                    <th>A receber</th>
                    <th>A pagar</th>
                    <th>Saldo final</th>
                    <th>Precisa receber</th>
                    <th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {meuFuturo.meses.map((m: MeuFuturoMes) => (
                    <tr key={m.mes} className={STATUS_CLASSE[m.status]}>
                      <td>{monthLabel(m.mes)}</td>
                      <td>{fmtBRL(m.saldoInicial)}</td>
                      <td className="val-green">+{fmtBRL(m.aReceber)}</td>
                      <td className="val-red">-{fmtBRL(m.aPagar)}</td>
                      <td className={m.saldoFinal >= 0 ? 'val-green' : 'val-red'}>{fmtBRL(m.saldoFinal)}</td>
                      <td>{m.precisaReceber > 0 ? fmtBRL(m.precisaReceber) : '—'}</td>
                      <td>{STATUS_LABEL[m.status]}</td>
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
