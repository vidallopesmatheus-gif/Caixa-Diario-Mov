import { fmtBRL } from '../../../utils/format'
import type { AndamentoConquista } from '../../../types'

function fmtMesAno(iso: string): string {
  return new Date(iso.slice(0, 10) + 'T12:00:00')
    .toLocaleDateString('pt-BR', { month: 'short', year: 'numeric' })
    .replace('. de ', '/').replace(' de ', '/').replace('.', '')
}

function mesSeguinte(iso: string): string {
  const d = new Date(iso.slice(0, 10) + 'T12:00:00')
  d.setMonth(d.getMonth() + 1)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-01`
}

interface Props {
  andamento: AndamentoConquista
}

/** Money Vision: "Pago R$ 409,16 de R$ 4.091,60 (10%) · Nubank · 1 de 10 · termina jul/2027". */
export default function AndamentoConquistaResumo({ andamento }: Props) {
  const percentual = andamento.valorTotal > 0 ? Math.round((andamento.valorPago / andamento.valorTotal) * 100) : 0
  const qtd = andamento.parcelamentos.length

  return (
    <div style={{ fontSize: 13, color: 'var(--tx2)', marginTop: 6, display: 'flex', flexDirection: 'column', gap: 2 }}>
      <div>
        Pago {fmtBRL(andamento.valorPago)} de {fmtBRL(andamento.valorTotal)} ({percentual}%)
        {qtd > 0 && <> · {qtd} {qtd === 1 ? 'parcelamento' : 'parcelamentos'}</>}
      </div>
      {andamento.parcelamentos.map(p => (
        <div key={p.id} style={{ color: 'var(--tx3)' }}>
          {p.banco ?? p.descricao} · {p.parcelasPagas} de {p.totalParcelas}
          {p.parcelasPagas < p.totalParcelas ? ` · termina ${fmtMesAno(p.dataUltimaParcela)}` : ' · quitado'}
        </div>
      ))}
      {andamento.dataTermino && andamento.valorMensal > 0 && (
        <div style={{ color: '#34c759' }}>
          A partir de {fmtMesAno(mesSeguinte(andamento.dataTermino))} sobram {fmtBRL(andamento.valorMensal)} por mês
        </div>
      )}
    </div>
  )
}
