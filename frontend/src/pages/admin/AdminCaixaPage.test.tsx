// frontend/src/pages/admin/AdminCaixaPage.test.tsx
import { render, screen, fireEvent } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import AdminCaixaPage from './AdminCaixaPage'

vi.mock('../client/ClientCaixaPage', () => ({ default: () => <div>ClientCaixaPage</div> }))
vi.mock('../client/ClientHistoricoPage', () => ({ default: () => <div>ClientHistoricoPage</div> }))

function renderWithParam(clienteId?: string) {
  const path = clienteId ? `/admin/caixa/${clienteId}` : '/admin/caixa'
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/admin/caixa/:clienteId" element={<AdminCaixaPage />} />
        <Route path="/admin/caixa" element={<AdminCaixaPage />} />
      </Routes>
    </MemoryRouter>
  )
}

test('exibe aviso quando clienteId está ausente', () => {
  renderWithParam()
  expect(screen.getByText(/Selecione um cliente/)).toBeInTheDocument()
})

test('exibe ClientCaixaPage por padrão quando clienteId presente', () => {
  renderWithParam('id-cliente-123')
  expect(screen.getByText('ClientCaixaPage')).toBeInTheDocument()
})

test('botão Caixa tem classe btn-confirm por padrão', () => {
  renderWithParam('id-cliente-123')
  const btnCaixa = screen.getByRole('button', { name: /Caixa/ })
  expect(btnCaixa.className).toContain('btn-confirm')
})

test('troca para ClientHistoricoPage ao clicar em Histórico', () => {
  renderWithParam('id-cliente-123')
  fireEvent.click(screen.getByText(/Histórico/))
  expect(screen.getByText('ClientHistoricoPage')).toBeInTheDocument()
})

test('botão Histórico fica ativo após clique', () => {
  renderWithParam('id-cliente-123')
  fireEvent.click(screen.getByText(/Histórico/))
  const btnHist = screen.getByText(/Histórico/)
  expect(btnHist.className).toContain('btn-confirm')
})
