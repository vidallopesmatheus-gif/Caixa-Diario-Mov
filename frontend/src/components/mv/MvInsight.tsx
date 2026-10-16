import type { ReactNode } from 'react'
import type { MvTone } from './MvHero'
import './mv.css'

interface Props {
  icon?: string
  text: ReactNode
  tone?: MvTone
}

/** Frase de leitura em linguagem simples ("o que isso quer dizer") — ícone + borda colorida. */
export default function MvInsight({ icon = '💬', text, tone = 'neutral' }: Props) {
  return (
    <div className={`mv-insight mv-tone-${tone}`}>
      <span className="mv-insight-icon" aria-hidden="true">{icon}</span>
      <span>{text}</span>
    </div>
  )
}
