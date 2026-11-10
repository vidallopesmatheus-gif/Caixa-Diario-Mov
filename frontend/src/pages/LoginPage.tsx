import { useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'
import { login as apiLogin } from '../api/auth'
import './LoginPage.css'

// Só caminho interno do próprio app ("/banco/123") — nunca "//outro-site" nem URL absoluta,
// senão o ?voltar= viraria um redirecionamento aberto pra qualquer domínio.
function rotaDeVoltaSegura(voltar: string | null): string | null {
  if (!voltar || !voltar.startsWith('/') || voltar.startsWith('//') || voltar.startsWith('/login')) return null
  return voltar
}

export default function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const [params] = useSearchParams()
  // Money Vision ("desloga toda hora"): o apiFetch manda pra cá com ?expirou=1&voltar=<rota>.
  const sessaoExpirou = params.get('expirou') === '1'
  const voltar = rotaDeVoltaSegura(params.get('voltar'))
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
      // A rota de volta só vale pra área do próprio perfil (um admin não volta pra tela de cliente).
      const areaDoPerfil = res.dados.perfil === 'admin' ? voltar?.startsWith('/admin') : !voltar?.startsWith('/admin')
      navigate(voltar && areaDoPerfil ? voltar : res.dados.perfil === 'admin' ? '/admin/overview' : '/caixa', { replace: true })
    } catch (e: unknown) {
      setErro(e instanceof Error ? e.message : 'Erro ao fazer login')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="login-wrap">
      <div className="login-box">
        <div className="login-logo">
          <h1>💰 Money Vision</h1>
          <p className="login-subtitulo">Caixa Diário</p>
          <p>Sistema de controle financeiro</p>
        </div>
        {sessaoExpirou && !erro && (
          <div className="err-box" role="status">Sua sessão expirou. Entre de novo para continuar.</div>
        )}
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
