import { Routes, Route, Navigate } from 'react-router-dom'
import SubTabsBar from '../../components/Layout/SubTabsBar'
import ClientContasPage from './ClientContasPage'
import ClientFinanciamentosPage from './ClientFinanciamentosPage'

const TABS = [
  { to: 'a-pagar-receber', label: 'A pagar/receber' },
  { to: 'financiamentos', label: 'Financiamentos' },
]

/** Hub de "Contas" (Fase 6.7): a tela de pendências de sempre + a nova de financiamentos/
 * empréstimos/consórcios/parcelamentos, como subabas — ClientContasPage não é tocado. */
export default function ContasPage() {
  return (
    <>
      <SubTabsBar basePath="/contas" tabs={TABS} />
      <Routes>
        <Route path="a-pagar-receber" element={<ClientContasPage />} />
        <Route path="financiamentos" element={<ClientFinanciamentosPage />} />
        <Route path="*" element={<Navigate to="/contas/a-pagar-receber" replace />} />
      </Routes>
    </>
  )
}
