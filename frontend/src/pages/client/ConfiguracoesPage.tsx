import { Routes, Route, Navigate } from 'react-router-dom'
import SubTabsBar from '../../components/Layout/SubTabsBar'
import CategoriasPage from './configuracoes/CategoriasPage'
import ContasBancariasCrudPage from './configuracoes/ContasBancariasCrudPage'
import RegrasCategorizacaoPage from './configuracoes/RegrasCategorizacaoPage'
import ConfiguracaoFinanceiraPage from './configuracoes/ConfiguracaoFinanceiraPage'

const TABS = [
  { to: 'categorias', label: 'Plano de Contas' },
  { to: 'contas-bancarias', label: 'Contas Bancárias' },
  { to: 'regras', label: 'Regras de Categorização' },
  { to: 'financeiro', label: 'Financeiro' },
]

export default function ConfiguracoesPage() {
  return (
    <>
      <SubTabsBar basePath="/configuracoes" tabs={TABS} />
      <Routes>
        <Route path="categorias" element={<CategoriasPage />} />
        <Route path="contas-bancarias" element={<ContasBancariasCrudPage />} />
        <Route path="regras" element={<RegrasCategorizacaoPage />} />
        <Route path="financeiro" element={<ConfiguracaoFinanceiraPage />} />
        <Route path="*" element={<Navigate to="/configuracoes/categorias" replace />} />
      </Routes>
    </>
  )
}
