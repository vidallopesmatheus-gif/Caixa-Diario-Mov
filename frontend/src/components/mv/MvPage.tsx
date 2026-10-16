import type { ReactNode } from 'react'
import './mv.css'

interface Props {
  children: ReactNode
}

/** Container "cockpit" das telas de análise — fundo navy com gradiente radial e grade sutil.
 * Sempre escuro, independente do tema claro/escuro do resto do app. */
export default function MvPage({ children }: Props) {
  return <div className="mv-page">{children}</div>
}
