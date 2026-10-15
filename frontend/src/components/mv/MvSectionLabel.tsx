import './mv.css'

interface Props {
  children: string
}

/** Rótulo de seção — mono, maiúsculo, espaçado ("RESUMO", "CONVERSÕES"). */
export default function MvSectionLabel({ children }: Props) {
  return <span className="mv-section-label">{children}</span>
}
