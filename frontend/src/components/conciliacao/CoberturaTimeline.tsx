import { useMemo } from 'react'
import { fmtBRL, fmtDate, todayISO } from '../../utils/format'
import type { CoberturaExtrato } from '../../types'
import './Conciliacao.css'

// Mais que isso vira uma régua ilegível — o que importa pra conciliar é o passado recente.
const MAX_MESES = 12
const NOMES_MES = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez']

function isoDe(ano: number, mes: number, dia: number): string {
  return `${ano}-${String(mes).padStart(2, '0')}-${String(dia).padStart(2, '0')}`
}

interface Props {
  cobertura: CoberturaExtrato
  /** Clique numa lacuna: filtra o extrato pelo período sem extrato. */
  onSelecionarPeriodo: (de: string, ate: string) => void
}

/** Money Vision: dias com extrato importado (azul), lacunas (cinza) e pontos conciliados (✅/⚠). */
export default function CoberturaTimeline({ cobertura, onSelecionarPeriodo }: Props) {
  const hoje = todayISO()
  const cobertos = useMemo(() => new Set(cobertura.diasComExtrato), [cobertura.diasComExtrato])
  const pontoPorDia = useMemo(() => new Map(cobertura.pontos.map(p => [p.data, p])), [cobertura.pontos])

  const meses = useMemo(() => {
    const datas = [...cobertura.diasComExtrato, ...cobertura.saldosPorDia.map(s => s.data)].filter(d => d <= hoje).sort()
    if (datas.length === 0) return []
    const [hy, hm] = hoje.split('-').map(Number)
    const [py, pm] = datas[0].split('-').map(Number)
    const total = Math.min(MAX_MESES, (hy - py) * 12 + (hm - pm) + 1)
    return Array.from({ length: total }, (_, i) => {
      const idx = hy * 12 + (hm - 1) - (total - 1 - i)
      return { ano: Math.floor(idx / 12), mes: (idx % 12) + 1 }
    })
  }, [cobertura.diasComExtrato, cobertura.saldosPorDia, hoje])

  // Lacuna contínua que contém o dia clicado, sem sair do mês (o clique é num mês só).
  function selecionarLacuna(ano: number, mes: number, dia: number) {
    const ultimoDia = new Date(ano, mes, 0).getDate()
    let inicio = dia
    let fim = dia
    while (inicio > 1 && !cobertos.has(isoDe(ano, mes, inicio - 1))) inicio--
    while (fim < ultimoDia && !cobertos.has(isoDe(ano, mes, fim + 1)) && isoDe(ano, mes, fim + 1) <= hoje) fim++
    onSelecionarPeriodo(isoDe(ano, mes, inicio), isoDe(ano, mes, fim))
  }

  if (meses.length === 0) return null

  return (
    <details className="conc-timeline">
      <summary>Cobertura do extrato</summary>
      <div className="conc-timeline-legenda">
        <span><i className="conc-dia conc-dia--coberto" /> com extrato</span>
        <span><i className="conc-dia conc-dia--lacuna" /> sem extrato</span>
        <span>✅ conciliado com o banco · ⚠ diferença</span>
      </div>
      {meses.map(({ ano, mes }) => {
        const dias = new Date(ano, mes, 0).getDate()
        return (
          <div key={`${ano}-${mes}`} className="conc-timeline-mes">
            <span className="conc-timeline-rotulo">{NOMES_MES[mes - 1]}/{ano}</span>
            <div className="conc-timeline-dias">
              {Array.from({ length: dias }, (_, i) => {
                const iso = isoDe(ano, mes, i + 1)
                const ponto = pontoPorDia.get(iso)
                if (iso > hoje) return <span key={iso} className="conc-dia conc-dia--futuro" aria-hidden="true" />
                const coberto = cobertos.has(iso)
                const titulo = `${fmtDate(iso)} — ${coberto ? 'com extrato' : 'sem extrato'}`
                  + (ponto ? ` · banco ${fmtBRL(ponto.saldoBanco)} ${ponto.conciliado ? '✅' : `⚠ ${fmtBRL(ponto.diferenca)}`}` : '')
                return coberto ? (
                  <span key={iso} className="conc-dia conc-dia--coberto" title={titulo}>{ponto ? (ponto.conciliado ? '✅' : '⚠') : ''}</span>
                ) : (
                  <button key={iso} type="button" className="conc-dia conc-dia--lacuna" title={titulo}
                    aria-label={`Sem extrato em ${fmtDate(iso)} — filtrar o período`}
                    onClick={() => selecionarLacuna(ano, mes, i + 1)}>
                    {ponto ? (ponto.conciliado ? '✅' : '⚠') : ''}
                  </button>
                )
              })}
            </div>
          </div>
        )
      })}
    </details>
  )
}
