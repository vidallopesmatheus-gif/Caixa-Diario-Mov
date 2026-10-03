import { Routes, Route, Navigate } from 'react-router-dom'
import SubTabsBar from '../../components/Layout/SubTabsBar'
import ContasBancariasCrudPage from './configuracoes/ContasBancariasCrudPage'
import RegrasCategorizacaoPage from './configuracoes/RegrasCategorizacaoPage'

// Plano de Contas (categorias/grupos) é compartilhado entre todos os clientes do escritório —
// gerenciar isso é responsabilidade do admin (ver /admin/categorias), não do cliente.
const TABS = [
  { to: 'contas-bancarias', label: 'Contas Bancárias' },
  { to: 'regras', label: 'Regras de Categorização' },
]

export default function ConfiguracoesPage() {
  return (
    <>
      <SubTabsBar basePath="/configuracoes" tabs={TABS} />
      <Routes>
        <Route path="contas-bancarias" element={<ContasBancariasCrudPage />} />
        <Route path="regras" element={<RegrasCategorizacaoPage />} />
        <Route path="*" element={<Navigate to="/configuracoes/contas-bancarias" replace />} />
      </Routes>
    </>
  )
}
