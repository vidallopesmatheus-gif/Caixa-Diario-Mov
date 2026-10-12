import { Routes, Route, Navigate } from 'react-router-dom'
import SubTabsBar from '../../components/Layout/SubTabsBar'
import ClientDrePage from './ClientDrePage'
import ClientProjecaoPage from './ClientProjecaoPage'
import ClientGraficoPage from './ClientGraficoPage'
import ClientPrevistoRealizadoPage from './ClientPrevistoRealizadoPage'
import ClientMeuFuturoPage from './ClientMeuFuturoPage'

const TABS = [
  { to: 'dre', label: 'DRE' },
  { to: 'projecao', label: 'Projeção' },
  { to: 'futuro', label: 'Meu Futuro' },
  { to: 'previsto-realizado', label: 'Previsto × Realizado' },
  { to: 'indicadores', label: 'Indicadores' },
]

export default function ResultadosPage() {
  return (
    <>
      <SubTabsBar basePath="/resultados" tabs={TABS} />
      <Routes>
        <Route path="dre" element={<ClientDrePage />} />
        <Route path="projecao" element={<ClientProjecaoPage />} />
        <Route path="futuro" element={<ClientMeuFuturoPage />} />
        <Route path="previsto-realizado" element={<ClientPrevistoRealizadoPage />} />
        <Route path="indicadores" element={<ClientGraficoPage />} />
        <Route path="*" element={<Navigate to="/resultados/dre" replace />} />
      </Routes>
    </>
  )
}
