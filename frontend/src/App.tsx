import { lazy, Suspense } from 'react'
import { BrowserRouter, Routes, Route, Navigate, useParams } from 'react-router-dom'
import { AuthProvider, useAuth } from './contexts/AuthContext'
import LoginPage from './pages/LoginPage'
import Layout from './components/Layout/Layout'
import InstallPrompt from './components/InstallPrompt'
import PwaUpdateBanner from './components/PwaUpdateBanner'

// Money Vision (performance): cada tela vira um chunk próprio, baixado só quando é aberta — antes
// o app inteiro (~900 KB) vinha num arquivo só já na tela de login. Login e Layout ficam no
// bundle inicial: são o que aparece primeiro.
const AdminOverviewPage = lazy(() => import('./pages/admin/AdminOverviewPage'))
const AdminClientsPage = lazy(() => import('./pages/admin/AdminClientsPage'))
const AdminCaixaPage = lazy(() => import('./pages/admin/AdminCaixaPage'))
const ClientCaixaPage = lazy(() => import('./pages/client/ClientCaixaPage'))
const ContasPage = lazy(() => import('./pages/client/ContasPage'))
const ClientDashboardPage = lazy(() => import('./pages/client/ClientDashboardPage'))
const ClientBancoPage = lazy(() => import('./pages/client/ClientBancoPage'))
const ClientContaDetalhePage = lazy(() => import('./pages/client/ClientContaDetalhePage'))
const ClientFaturasCartaoPage = lazy(() => import('./pages/client/ClientFaturasCartaoPage'))
const ClientExtratoRevisaoPage = lazy(() => import('./pages/client/ClientExtratoRevisaoPage'))
const ClientTransferenciasSemParPage = lazy(() => import('./pages/client/ClientTransferenciasSemParPage'))
const ResultadosPage = lazy(() => import('./pages/client/ResultadosPage'))
const RelatoriosPage = lazy(() => import('./pages/client/RelatoriosPage'))
const ConfiguracoesPage = lazy(() => import('./pages/client/ConfiguracoesPage'))
const PortalConciliacaoPage = lazy(() => import('./pages/portal/PortalConciliacaoPage'))

function CarregandoTela() {
  return <p style={{ padding: 24, color: 'var(--tx3)' }} role="status">Carregando...</p>
}

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
        <Suspense fallback={<CarregandoTela />}>
        <Routes>
          <Route path="/admin/overview" element={<AdminOverviewPage />} />
          <Route path="/admin/clientes" element={<AdminClientsPage />} />
          <Route path="/admin/caixa/:clienteId" element={<AdminCaixaPage />} />
          <Route path="*" element={<Navigate to="/admin/overview" replace />} />
        </Routes>
        </Suspense>
      </Layout>
    )
  }

  return (
    <Layout>
      <Suspense fallback={<CarregandoTela />}>
      <Routes>
        <Route path="/dashboard"  element={<ClientDashboardPage />} />
        <Route path="/caixa"      element={<ClientCaixaPage />} />
        <Route path="/contas/*"   element={<ContasPage />} />

        {/* Banco: operação (extrato, importação) — cadastro fica em Configurações */}
        <Route path="/banco" element={<ClientBancoPage />} />
        <Route path="/banco/transferencias-sem-par" element={<ClientTransferenciasSemParPage />} />
        <Route path="/banco/:contaId" element={<ClientContaDetalhePage />} />
        <Route path="/banco/:contaId/faturas" element={<ClientFaturasCartaoPage />} />
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
      </Suspense>
    </Layout>
  )
}

export default function App() {
  return (
    <>
      <AuthProvider>
        <BrowserRouter>
          <Suspense fallback={<CarregandoTela />}>
          <Routes>
            <Route path="/login" element={<LoginPage />} />
            {/* Portal público (sem login) — link que o cliente abre pra classificar pendentes */}
            <Route path="/portal/conciliacao/:token" element={<PortalConciliacaoPage />} />
            <Route path="/*" element={<ProtectedRoutes />} />
          </Routes>
          </Suspense>
        </BrowserRouter>
      </AuthProvider>
      <PwaUpdateBanner />
      <InstallPrompt />
    </>
  )
}
