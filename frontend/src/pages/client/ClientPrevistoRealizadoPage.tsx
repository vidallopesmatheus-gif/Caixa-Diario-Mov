import { useState, useEffect, useCallback } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { obterPrevistoRealizado } from '../../api/relatorios'
import type { PrevistoRealizado } from '../../api/relatorios'
import { fmtBRL } from '../../utils/format'
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
