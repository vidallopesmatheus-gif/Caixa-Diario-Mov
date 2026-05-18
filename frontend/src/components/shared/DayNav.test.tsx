import { render, screen, fireEvent } from '@testing-library/react'
import DayNav from './DayNav'

test('exibe data formatada', () => {
  render(<DayNav date="2026-05-15" onPrev={() => {}} onNext={() => {}} />)
  expect(screen.getByText('15/05/2026')).toBeInTheDocument()
})

test('chama onPrev ao clicar em ←', () => {
  const onPrev = vi.fn()
  render(<DayNav date="2026-05-15" onPrev={onPrev} onNext={() => {}} />)
  fireEvent.click(screen.getByText('←'))
  expect(onPrev).toHaveBeenCalledTimes(1)
})

test('chama onNext ao clicar em →', () => {
  const onNext = vi.fn()
  render(<DayNav date="2026-05-15" onPrev={() => {}} onNext={onNext} />)
  fireEvent.click(screen.getByText('→'))
  expect(onNext).toHaveBeenCalledTimes(1)
})
