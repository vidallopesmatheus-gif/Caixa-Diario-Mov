import { Component, type ReactNode } from 'react'

interface Props { children: ReactNode }
interface State { error: Error | null }

export default class ErrorBoundary extends Component<Props, State> {
  state: State = { error: null }

  static getDerivedStateFromError(error: Error): State {
    return { error }
  }

  render() {
    const { error } = this.state
    if (!error) return this.props.children
    return (
      <div style={{ padding: 40, textAlign: 'center', color: '#ff6b6b' }}>
        <h2>Algo deu errado</h2>
        <p style={{ color: '#888', marginBottom: 24 }}>{error.message}</p>
        <button
          style={{ padding: '10px 24px', borderRadius: 8, border: '1px solid #ff6b6b', background: 'none', color: '#ff6b6b', cursor: 'pointer' }}
          onClick={() => { localStorage.removeItem('user'); localStorage.removeItem('token'); window.location.href = '/login' }}
        >
          Limpar sessão e voltar ao login
        </button>
      </div>
    )
  }
}
