import { NavLink } from 'react-router-dom'

interface SubTab {
  to: string
  label: string
}

interface Props {
  basePath: string
  tabs: SubTab[]
}

/** Barra de subabas usada dentro de hubs como Resultados, Relatórios e Configurações. */
export default function SubTabsBar({ basePath, tabs }: Props) {
  return (
    <div className="subtabs-bar">
      {tabs.map(t => (
        <NavLink key={t.to} to={`${basePath}/${t.to}`}
          className={({ isActive }) => `subtab-btn${isActive ? ' active' : ''}`}>
          {t.label}
        </NavLink>
      ))}
    </div>
  )
}
