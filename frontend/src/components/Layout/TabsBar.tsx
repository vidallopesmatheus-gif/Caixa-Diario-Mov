import { NavLink } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'

export default function TabsBar() {
  const { user } = useAuth()

  if (!user) return null

  const adminTabs = [
    { to: '/admin/overview', label: '🏠 Visão Geral' },
    { to: '/admin/clientes', label: '👥 Clientes' },
  ]
  const clientTabs = [
    { to: '/dashboard', label: 'Dashboard' },
    { to: '/caixa', label: 'Caixa' },
    { to: '/banco', label: 'Banco' },
    { to: '/contas', label: 'Contas' },
    { to: '/resultados', label: 'Resultados' },
    { to: '/relatorios', label: 'Relatórios' },
    { to: '/configuracoes', label: 'Configurações' },
  ]
  const tabs = user.perfil === 'admin' ? adminTabs : clientTabs

  return (
    <div className="tabs-bar">
      {tabs.map(t => (
        <NavLink key={t.to} to={t.to}
          className={({ isActive }) => `tab-btn${isActive ? ' active' : ''}`}>
          {t.label}
        </NavLink>
      ))}
    </div>
  )
}
