import { BrowserRouter, Routes, Route, Navigate, useParams } from 'react-router-dom'
import { AuthProvider, useAuth } from './contexts/AuthContext'
import LoginPage from './pages/LoginPage'
import AdminOverviewPage from './pages/admin/AdminOverviewPage'
import AdminClientsPage from './pages/admin/AdminClientsPage'
import AdminCaixaPage from './pages/admin/AdminCaixaPage'
import ClientCaixaPage from './pages/client/ClientCaixaPage'
import ClientContasPage from './pages/client/ClientContasPage'
import ClientDashboardPage from './pages/client/ClientDashboardPage'
import ClientBancoPage from './pages/client/ClientBancoPage'
import ClientContaDetalhePage from './pages/client/ClientContaDetalhePage'
import ClientExtratoRevisaoPage from './pages/client/ClientExtratoRevisaoPage'
import ResultadosPage from './pages/client/ResultadosPage'
import RelatoriosPage from './pages/client/RelatoriosPage'
import ConfiguracoesPage from './pages/client/ConfiguracoesPage'
import Layout from './components/Layout/Layout'
import InstallPrompt from './components/InstallPrompt'

/** Redireciona uma rota antiga com :contaId para o novo path equivalente sob /banco. */
function RedirectContaId({ para }: { para: (contaId: string) => string }) {
  const { contaId } = useParams()
  return <Navigate to={para(contaId ?? '')} replace />
}

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

        {/* Banco: operação (extrato, importação) — cadastro fica em Configurações */}
        <Route path="/banco" element={<ClientBancoPage />} />
        <Route path="/banco/:contaId" element={<ClientContaDetalhePage />} />
        <Route path="/banco/extrato/:contaId" element={<ClientExtratoRevisaoPage />} />

        {/* Resultados: DRE, Projeção, Indicadores (subabas na URL) */}
        <Route path="/resultados/*" element={<ResultadosPage />} />

        {/* Relatórios: Histórico, Exportar (subabas na URL) */}
        <Route path="/relatorios/*" element={<RelatoriosPage />} />

        {/* Configurações: Plano de Contas, Contas Bancárias (subabas na URL) */}
        <Route path="/configuracoes/*" element={<ConfiguracoesPage />} />

        {/* Redirects de compatibilidade — rotas antigas continuam funcionando */}
        <Route path="/dre" element={<Navigate to="/resultados/dre" replace />} />
        <Route path="/projecao" element={<Navigate to="/resultados/projecao" replace />} />
        <Route path="/grafico" element={<Navigate to="/resultados/indicadores" replace />} />
        <Route path="/historico" element={<Navigate to="/relatorios/historico" replace />} />
        <Route path="/exportar" element={<Navigate to="/relatorios/exportar" replace />} />
        <Route path="/contas-bancarias" element={<Navigate to="/banco" replace />} />
        <Route path="/contas-bancarias/:contaId" element={<RedirectContaId para={id => `/banco/${id}`} />} />
        <Route path="/extrato/:contaId" element={<RedirectContaId para={id => `/banco/extrato/${id}`} />} />

        <Route path="*" element={<Navigate to="/dashboard" replace />} />
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
