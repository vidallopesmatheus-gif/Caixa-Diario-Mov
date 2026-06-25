import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { AuthProvider, useAuth } from './contexts/AuthContext'
import LoginPage from './pages/LoginPage'
import AdminOverviewPage from './pages/admin/AdminOverviewPage'
import AdminClientsPage from './pages/admin/AdminClientsPage'
import AdminCaixaPage from './pages/admin/AdminCaixaPage'
import ClientCaixaPage from './pages/client/ClientCaixaPage'
import ClientHistoricoPage from './pages/client/ClientHistoricoPage'
import ClientGraficoPage from './pages/client/ClientGraficoPage'
import ClientContasPage from './pages/client/ClientContasPage'
import ClientDashboardPage from './pages/client/ClientDashboardPage'
import ClientExportacaoPage from './pages/client/ClientExportacaoPage'
import ClientContasBancariasPage from './pages/client/ClientContasBancariasPage'
import ClientExtratoRevisaoPage from './pages/client/ClientExtratoRevisaoPage'
import ClientDrePage from './pages/client/ClientDrePage'
import ClientProjecaoPage from './pages/client/ClientProjecaoPage'
import Layout from './components/Layout/Layout'
import InstallPrompt from './components/InstallPrompt'

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
        <Route path="/dashboard"  element={<ClientDashboardPage />} />
        <Route path="/caixa"      element={<ClientCaixaPage />} />
        <Route path="/contas"     element={<ClientContasPage />} />
        <Route path="/historico"  element={<ClientHistoricoPage />} />
        <Route path="/grafico"    element={<ClientGraficoPage />} />
        <Route path="/exportar"         element={<ClientExportacaoPage />} />
        <Route path="/contas-bancarias" element={<ClientContasBancariasPage />} />
        <Route path="/extrato/:contaId" element={<ClientExtratoRevisaoPage />} />
        <Route path="/dre"       element={<ClientDrePage />} />
        <Route path="/projecao" element={<ClientProjecaoPage />} />
        <Route path="*"           element={<Navigate to="/dashboard" replace />} />
      </Routes>
    </Layout>
  )
}

export default function App() {
  return (
    <>
      <AuthProvider>
        <BrowserRouter>
          <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route path="/*" element={<ProtectedRoutes />} />
          </Routes>
        </BrowserRouter>
      </AuthProvider>
      <InstallPrompt />
    </>
  )
}
