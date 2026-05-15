import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'
import { login as apiLogin } from '../api/auth'
import './LoginPage.css'

export default function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const [usuario, setUsuario] = useState('')
  const [senha, setSenha] = useState('')
  const [erro, setErro] = useState('')
  const [loading, setLoading] = useState(false)

  async function handleLogin(e: React.FormEvent) {
    e.preventDefault()
    setErro('')
    setLoading(true)
    try {
      const res = await apiLogin(usuario, senha)
      login(res.dados)
      navigate(res.dados.perfil === 'admin' ? '/admin/overview' : '/caixa')
    } catch (e: any) {
      setErro(e.message ?? 'Erro ao fazer login')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="login-wrap">
      <div className="login-box">
        <div className="login-logo">
          <h1>💰 Caixa Diário</h1>
          <p>Sistema de controle financeiro</p>
        </div>
        {erro && <div className="err-box">{erro}</div>}
        <form onSubmit={handleLogin}>
          <div className="field">
            <label>Usuário</label>
            <input value={usuario} onChange={e => setUsuario(e.target.value)}
              placeholder="seu usuário" autoComplete="username" required />
          </div>
          <div className="field">
            <label>Senha</label>
            <input type="password" value={senha} onChange={e => setSenha(e.target.value)}
              placeholder="sua senha" autoComplete="current-password" required />
          </div>
          <button className="btn-primary" type="submit" disabled={loading}>
            {loading ? 'Entrando...' : 'Entrar'}
          </button>
        </form>
      </div>
    </div>
  )
}
