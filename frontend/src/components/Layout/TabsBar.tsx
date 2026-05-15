import { NavLink } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'

export default function TabsBar() {
  const { user } = useAuth()

  if (!user) return null

  const adminTabs = [
    { to: '/admin/overview', label: '🏠 Visão Geral' },
    { to: '/admin/clientes', label: '👥 Clientes' },
    { to: '/admin/caixa',    label: '📋 Ver Caixa' },
  ]
  const clientTabs = [
    { to: '/caixa',     label: '📋 Caixa Diário' },
    { to: '/historico', label: '📈 Histórico' },
    { to: '/grafico',   label: '📊 Evolução' },
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
