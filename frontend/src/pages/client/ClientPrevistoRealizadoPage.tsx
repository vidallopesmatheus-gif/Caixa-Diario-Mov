import { useState, useEffect, useCallback, useMemo } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { obterPrevistoRealizado } from '../../api/relatorios'
import type { PrevistoRealizado } from '../../api/relatorios'
import { fmtBRL } from '../../utils/format'
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts'
import './ClientProjecao.css'

const MESES_ABREV = ['Jan','Fev','Mar','Abr','Mai','Jun','Jul','Ago','Set','Out','Nov','Dez']

function mesLabel(mes: string): string {
  const [ano, m] = mes.split('-')
  return `${MESES_ABREV[Number(m) - 1]}/${ano.slice(2)}`
}

function fmtPct(fracao: number): string {
  const sinal = fracao >= 0 ? '+' : ''
  return `${sinal}${(fracao * 100).toLocaleString('pt-BR', { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%`
}

function csvEscape(valor: string): string {
  return /[",;\n]/.test(valor) ? `"${valor.replace(/"/g, '""')}"` : valor
}

function exportarCsv(dados: PrevistoRealizado) {
  const linhasCsv = [['Tipo', 'Categoria', 'Mês', 'Previsto', 'Realizado', 'Variação %', 'Variação alta', 'Subiu 3 meses seguidos']]
  for (const linha of dados.linhas) {
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

export default function ClientPrevistoRealizadoPage() {
  const { user } = useAuth()
  const clienteId = user?.usuarioId ?? ''

  const [meses, setMeses] = useState<3 | 6 | 12>(6)
  const [incluirAvulsosVinculados, setIncluirAvulsosVinculados] = useState(false)
  const [dados, setDados] = useState<PrevistoRealizado | null>(null)
  const [loading, setLoading] = useState(false)
  const [erro, setErro] = useState('')

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

  // Soma previsto/realizado de todas as linhas (tipo × categoria) por mês, pra um gráfico de
  // linha único no topo — a tabela abaixo continua com o detalhe por categoria.
  const dadosGrafico = useMemo(() => {
    if (!dados) return []
    const porMes = new Map<string, { mes: string; previsto: number; realizado: number }>()
    for (const linha of dados.linhas) {
      for (const ponto of linha.meses) {
        const acumulado = porMes.get(ponto.mes) ?? { mes: ponto.mes, previsto: 0, realizado: 0 }
        acumulado.previsto += ponto.previsto
        acumulado.realizado += ponto.realizado
        porMes.set(ponto.mes, acumulado)
      }
    }
    return [...porMes.values()]
      .sort((a, b) => a.mes.localeCompare(b.mes))
      .map(p => ({ ...p, mes: mesLabel(p.mes) }))
  }, [dados])

  return (
    <>
      <div className="pj-header">
        <div>
          <h2 className="pj-titulo">📊 Previsto × Realizado</h2>
          <div className="pj-subtitulo">Contas fixas recorrentes: o que era esperado vs. o que de fato aconteceu no extrato</div>
        </div>
      </div>

      <div className="pj-controles">
        <div className="pj-janela-tabs">
          {([3, 6, 12] as const).map(m => (
            <button key={m} className={`pj-janela-btn${meses === m ? ' active' : ''}`} onClick={() => setMeses(m)}>
              {m} meses
            </button>
          ))}
        </div>
        <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 13, color: 'var(--tx2)', cursor: 'pointer' }}>
          <input
            type="checkbox"
            checked={incluirAvulsosVinculados}
            onChange={e => setIncluirAvulsosVinculados(e.target.checked)}
          />
          Incluir contas avulsas pagas por vínculo
        </label>
        <button className="btn-cancel" disabled={!dados || dados.linhas.length === 0} onClick={() => dados && exportarCsv(dados)}>
          📋 Exportar CSV
        </button>
      </div>

      {loading && <p className="pj-loading">Calculando...</p>}
      {erro && <p className="pj-erro">{erro}</p>}

      {!loading && dados && dados.linhas.length === 0 && (
        <p className="pj-vazio">Nenhuma conta fixa recorrente com título gerado no período — não há o que comparar ainda.</p>
      )}

      {!loading && dados && dados.linhas.length > 0 && (
        <div className="pj-grafico-container">
          <ResponsiveContainer width="100%" height="100%">
            <LineChart data={dadosGrafico}>
              <CartesianGrid strokeDasharray="3 3" stroke="var(--bd)" />
              <XAxis dataKey="mes" stroke="var(--tx3)" tick={{ fontSize: 12 }} />
              <YAxis stroke="var(--tx3)" tick={{ fontSize: 12 }} tickFormatter={v => `R$${(v / 1000).toFixed(0)}k`} />
              <Tooltip formatter={v => typeof v === 'number' ? fmtBRL(v) : String(v)} contentStyle={{ background: 'var(--bg-card)', border: '1px solid var(--bd)' }} />
              <Legend wrapperStyle={{ fontSize: 13 }} />
              <Line type="monotone" dataKey="previsto" name="Previsto" stroke="var(--tx3)" strokeWidth={2} dot={{ r: 3 }} />
              <Line type="monotone" dataKey="realizado" name="Realizado" stroke="#0a84ff" strokeWidth={2} dot={{ fill: '#0a84ff', r: 3 }} />
            </LineChart>
          </ResponsiveContainer>
        </div>
      )}

      {!loading && dados && dados.linhas.length > 0 && (
        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
            <thead>
              <tr style={{ textAlign: 'left', borderBottom: '1px solid var(--bd)' }}>
                <th style={{ padding: '8px 10px' }}>Tipo</th>
                <th style={{ padding: '8px 10px' }}>Categoria</th>
                <th style={{ padding: '8px 10px' }}>Mês</th>
                <th style={{ padding: '8px 10px', textAlign: 'right' }}>Previsto</th>
                <th style={{ padding: '8px 10px', textAlign: 'right' }}>Realizado</th>
                <th style={{ padding: '8px 10px', textAlign: 'right' }}>Variação</th>
              </tr>
            </thead>
            <tbody>
              {dados.linhas.flatMap(linha => linha.meses.map(ponto => (
                <tr key={`${linha.tipo}-${linha.categoria}-${ponto.mes}`} style={{ borderBottom: '1px solid var(--bd)' }}>
                  <td style={{ padding: '6px 10px' }}>{linha.tipo === 'Receber' ? '📥' : '📤'} {linha.tipo}</td>
                  <td style={{ padding: '6px 10px' }}>{linha.categoria}</td>
                  <td style={{ padding: '6px 10px' }}>{mesLabel(ponto.mes)}</td>
                  <td style={{ padding: '6px 10px', textAlign: 'right' }}>{fmtBRL(ponto.previsto)}</td>
                  <td style={{ padding: '6px 10px', textAlign: 'right' }}>{fmtBRL(ponto.realizado)}</td>
                  <td style={{ padding: '6px 10px', textAlign: 'right', color: ponto.variacaoAlta ? '#ff6b6b' : 'inherit', fontWeight: ponto.variacaoAlta ? 600 : 400 }}>
                    {ponto.variacaoPercentual != null ? fmtPct(ponto.variacaoPercentual) : '—'}
                    {ponto.subiuTresMesesSeguidos && <span title="Subiu 3 meses seguidos"> ⚠️↑</span>}
                  </td>
                </tr>
              )))}
            </tbody>
          </table>
        </div>
      )}
    </>
  )
}
