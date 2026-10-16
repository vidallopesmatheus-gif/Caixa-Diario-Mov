import type { ReactNode } from 'react'
import { useCountUp } from './useCountUp'
import { fmtBRL } from '../../utils/format'
import './mv.css'

export type MvTone = 'good' | 'bad' | 'warn' | 'neutral'

const COR: Record<MvTone, string> = {
  good: 'var(--success)',
  bad: 'var(--danger)',
  warn: 'var(--warning)',
  neutral: 'var(--tx1)',
}

interface Contraste {
  valor: number
  formatter?: (n: number) => string
}

interface ItemBreakdown {
  label: string
  valor: number
  formatter?: (n: number) => string
}

interface Props {
  eyebrow: string
  value: number
  formatter?: (n: number) => string
  tone?: MvTone
  /** Contraponto no estilo "266 vs 0" / "R$ 8.420 vs hoje R$ 12.986". */
  contraste?: Contraste
  /** Linha de detalhamento abaixo do número — ex.: "Disponível R$ X · Investido R$ Y". */
  breakdown?: ItemBreakdown[]
  /** Frase simples explicando o número — aceita <strong> pra destacar trechos. */
  caption?: ReactNode
  /** Linha de rodapé em mono — ex.: "LIDO EM 14/10/2026". */
  footnote?: string
}

/** Número gigante com contraponto opcional — a resposta da pergunta da tela, em 5 segundos. */
export default function MvHero({ eyebrow, value, formatter = fmtBRL, tone = 'neutral', contraste, breakdown, caption, footnote }: Props) {
  const exibido = useCountUp(value)
  const contrasteExibido = useCountUp(contraste?.valor ?? null)

  return (
    <div className="mv-hero">
      <div className="mv-hero-bar"><span /><span /><span /><span /></div>
      <span className="mv-hero-eyebrow">{eyebrow}</span>
      <div className="mv-hero-row">
        <span className="mv-hero-valor" style={{ color: COR[tone] }}>
          {formatter(exibido ?? value)}
        </span>
        {contraste && (
          <span className="mv-hero-contraste">
            <span className="mv-hero-vs">vs</span>
            {(contraste.formatter ?? formatter)(contrasteExibido ?? contraste.valor)}
          </span>
        )}
      </div>
      {breakdown && breakdown.length > 0 && (
        <div className="mv-hero-breakdown">
          {breakdown.map((b, i) => (
            <span key={i}>
              {i > 0 && <span className="mv-hero-breakdown-dot">·</span>}
              {b.label} <strong>{(b.formatter ?? formatter)(b.valor)}</strong>
            </span>
          ))}
        </div>
      )}
      {caption && <p className="mv-hero-caption">{caption}</p>}
      {footnote && <div className="mv-hero-footnote">{footnote}</div>}
    </div>
  )
}
