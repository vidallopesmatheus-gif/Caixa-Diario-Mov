import { useAuth } from '../../contexts/AuthContext'
import { useEffect, useState } from 'react'

export default function TopBar() {
  const { user, logout } = useAuth()
  const [light, setLight] = useState(() => localStorage.getItem('theme') === 'light')

  useEffect(() => {
    document.body.classList.toggle('light', light)
    localStorage.setItem('theme', light ? 'light' : 'dark')
  }, [light])

  if (!user) return null
  const displayName = user.nomeCompleto ?? user.nomeUsuario ?? ''
  const initials = displayName.split(' ').filter(Boolean).map((w: string) => w[0]).slice(0, 2).join('').toUpperCase()

  return (
    <div className="topbar">
      <div className="topbar-left">
        <span className="topbar-logo">💰 Caixa Diário</span>
        <span className={`badge-role ${user.perfil === 'admin' ? 'badge-admin' : 'badge-client'}`}>
          {user.perfil === 'admin' ? 'Admin' : 'Cliente'}
        </span>
      </div>
      <div className="topbar-right">
        <button className="btn-sm btn-theme" onClick={() => setLight(l => !l)}>
          {light ? '🌙' : '☀️'}
        </button>
        <div className="avatar">{initials}</div>
        <span style={{ fontSize: 14, fontWeight: 600 }}>{displayName}</span>
        <button className="btn-sm btn-logout" onClick={logout}>Sair</button>
      </div>
    </div>
  )
}
