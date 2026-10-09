import { NavLink } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'

export default function TabsBar() {
  const { user } = useAuth()

  if (!user) return null

  const adminTabs = [
    { to: '/admin/overview', label: '🏠 Visão Geral' },
    { to: '/admin/clientes', label: '👥 Clientes' },
  ]
  // Item 2.2: ícone próprio por aba — no menu mobile (barra inferior) o rótulo vira só o ícone
  // em cima e um texto pequeno embaixo, compacto o bastante pras 7 abas caberem sem sumir.
  const clientTabs = [
    { to: '/dashboard', label: 'Dashboard', icone: '📊' },
    { to: '/caixa', label: 'Caixa', icone: '💵' },
    { to: '/banco', label: 'Banco', icone: '🏦' },
    { to: '/contas', label: 'Contas', icone: '🧾' },
    { to: '/resultados', label: 'Resultados', icone: '📈' },
    { to: '/relatorios', label: 'Relatórios', icone: '📑' },
    { to: '/configuracoes', label: 'Configurações', icone: '⚙️' },
  ]
  const tabs = user.perfil === 'admin'
    ? adminTabs.map(t => ({ ...t, icone: '' }))
    : clientTabs

  return (
    <div className="tabs-bar">
      {tabs.map(t => (
        <NavLink key={t.to} to={t.to}
          className={({ isActive }) => `tab-btn${isActive ? ' active' : ''}`}>
          {t.icone && <span className="tab-btn-icone" aria-hidden="true">{t.icone}</span>}
          <span className="tab-btn-label">{t.label}</span>
        </NavLink>
      ))}
    </div>
  )
}
