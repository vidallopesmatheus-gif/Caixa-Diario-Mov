import { Routes, Route, Navigate } from 'react-router-dom'
import SubTabsBar from '../../components/Layout/SubTabsBar'
import ClientHistoricoPage from './ClientHistoricoPage'
import ClientExportacaoPage from './ClientExportacaoPage'

const TABS = [
  { to: 'historico', label: 'Histórico' },
  { to: 'exportar', label: 'Exportar' },
]

export default function RelatoriosPage() {
  return (
    <>
      <SubTabsBar basePath="/relatorios" tabs={TABS} />
      <Routes>
        <Route path="historico" element={<ClientHistoricoPage />} />
        <Route path="exportar" element={<ClientExportacaoPage />} />
        <Route path="*" element={<Navigate to="/relatorios/historico" replace />} />
      </Routes>
    </>
  )
}
