import { useState, type ReactNode } from 'react'
import './mv.css'

interface Props {
  label?: string
  children: ReactNode
  defaultOpen?: boolean
}

/** Accordion "Ver detalhes" — a tabela/lista detalhada nunca é conteúdo principal, só aparece
 * sob demanda. */
export default function MvDetails({ label = 'Ver detalhes', children, defaultOpen = false }: Props) {
  const [aberto, setAberto] = useState(defaultOpen)

  return (
    <div className="mv-details">
      <button type="button" className="mv-details-toggle" onClick={() => setAberto(a => !a)} aria-expanded={aberto}>
        <span className={`mv-details-caret${aberto ? ' mv-aberto' : ''}`}>▸</span>
        {aberto ? `Ocultar ${label.toLowerCase()}` : label}
      </button>
      {aberto && <div className="mv-details-body">{children}</div>}
    </div>
  )
}
