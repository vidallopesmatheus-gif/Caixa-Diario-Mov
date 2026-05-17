import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { AuthProvider, useAuth } from './contexts/AuthContext'
import LoginPage from './pages/LoginPage'
import AdminOverviewPage from './pages/admin/AdminOverviewPage'
import AdminClientsPage from './pages/admin/AdminClientsPage'
import AdminCaixaPage from './pages/admin/AdminCaixaPage'
import ClientCaixaPage from './pages/client/ClientCaixaPage'
import ClientHistoricoPage from './pages/client/ClientHistoricoPage'
import ClientGraficoPage from './pages/client/ClientGraficoPage'
import Layout from './components/Layout/Layout'

function ProtectedRoutes() {
  const { user } = useAuth()
  if (!user) return <Navigate to="/login" replace />

  if (user.perfil === 'admin') {
    return (
      <Layout>
        <Routes>
          <Route path="/admin/overview" element={<AdminOverviewPage />} />
          <Route path="/admin/clientes" element={<AdminClientsPage />} />
          <Route path="/admin/caixa/:clienteId" element={<AdminCaixaPage />} />
          <Route path="*" element={<Navigate to="/admin/overview" replace />} />
        </Routes>
      </Layout>
    )
  }

  return (
    <Layout>
      <Routes>
        <Route path="/caixa" element={<ClientCaixaPage />} />
        <Route path="/historico" element={<ClientHistoricoPage />} />
        <Route path="/grafico" element={<ClientGraficoPage />} />
        <Route path="*" element={<Navigate to="/caixa" replace />} />
      </Routes>
    </Layout>
  )
}

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/*" element={<ProtectedRoutes />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  )
}
