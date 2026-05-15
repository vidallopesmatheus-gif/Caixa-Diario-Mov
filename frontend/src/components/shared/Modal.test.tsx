import { render, screen, fireEvent } from '@testing-library/react'
import Modal from './Modal'

test('não renderiza quando open=false', () => {
  render(<Modal open={false} title="Teste" onClose={() => {}}>conteúdo</Modal>)
  expect(screen.queryByText('Teste')).not.toBeInTheDocument()
})

test('renderiza título e conteúdo quando open=true', () => {
  render(<Modal open={true} title="Meu Modal" onClose={() => {}}>corpo aqui</Modal>)
  expect(screen.getByText('Meu Modal')).toBeInTheDocument()
  expect(screen.getByText('corpo aqui')).toBeInTheDocument()
})

test('chama onClose ao clicar no overlay', () => {
  const onClose = vi.fn()
  const { container } = render(<Modal open={true} title="X" onClose={onClose}>y</Modal>)
  fireEvent.click(container.firstChild!)
  expect(onClose).toHaveBeenCalledTimes(1)
})
