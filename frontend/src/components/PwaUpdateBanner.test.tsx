import { render, screen, fireEvent } from '@testing-library/react'
import PwaUpdateBanner from './PwaUpdateBanner'
import * as pwaRegister from 'virtual:pwa-register/react'

vi.mock('virtual:pwa-register/react', () => ({ useRegisterSW: vi.fn() }))

function mockUseRegisterSW(needRefresh: boolean, updateServiceWorker = vi.fn()) {
  vi.mocked(pwaRegister.useRegisterSW).mockReturnValue({
    needRefresh: [needRefresh, vi.fn()],
    offlineReady: [false, vi.fn()],
    updateServiceWorker,
  })
}

test('não renderiza nada quando não há atualização pendente', () => {
  mockUseRegisterSW(false)
  render(<PwaUpdateBanner />)
  expect(screen.queryByText('Nova versão disponível')).not.toBeInTheDocument()
})

test('exibe o banner quando há uma atualização pendente (needRefresh)', () => {
  mockUseRegisterSW(true)
  render(<PwaUpdateBanner />)
  expect(screen.getByText('Nova versão disponível')).toBeInTheDocument()
  expect(screen.getByText('Atualizar')).toBeInTheDocument()
})

test('clicar em Atualizar chama updateServiceWorker', () => {
  const updateServiceWorker = vi.fn()
  mockUseRegisterSW(true, updateServiceWorker)
  render(<PwaUpdateBanner />)
  fireEvent.click(screen.getByText('Atualizar'))
  expect(updateServiceWorker).toHaveBeenCalledWith(true)
})
