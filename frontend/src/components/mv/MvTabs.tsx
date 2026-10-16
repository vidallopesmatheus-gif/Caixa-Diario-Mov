import './mv.css'

interface Opcao<T extends string> {
  valor: T
  label: string
}

interface Props<T extends string> {
  opcoes: Opcao<T>[]
  ativa: T
  onChange: (v: T) => void
}

/** Abas em pílula — a ativa preenchida em gradiente azul. Rolável horizontalmente em telas estreitas. */
export default function MvTabs<T extends string>({ opcoes, ativa, onChange }: Props<T>) {
  return (
    <div className="mv-tabs" role="tablist">
      {opcoes.map(o => (
        <button
          key={o.valor}
          type="button"
          role="tab"
          aria-selected={ativa === o.valor}
          className={`mv-tab${ativa === o.valor ? ' mv-tab-ativa' : ''}`}
          onClick={() => onChange(o.valor)}
        >
          {o.label}
        </button>
      ))}
    </div>
  )
}
