import { Routes, Route, Navigate } from 'react-router-dom'
import SubTabsBar from '../../components/Layout/SubTabsBar'
import CategoriasPage from './configuracoes/CategoriasPage'
import ContasBancariasCrudPage from './configuracoes/ContasBancariasCrudPage'

const TABS = [
  { to: 'categorias', label: 'Plano de Contas' },
  { to: 'contas-bancarias', label: 'Contas Bancárias' },
]

export default function ConfiguracoesPage() {
  return (
    <>
      <SubTabsBar basePath="/configuracoes" tabs={TABS} />
      <Routes>
        <Route path="categorias" element={<CategoriasPage />} />
        <Route path="contas-bancarias" element={<ContasBancariasCrudPage />} />
        <Route path="*" element={<Navigate to="/configuracoes/categorias" replace />} />
      </Routes>
    </>
  )
}
