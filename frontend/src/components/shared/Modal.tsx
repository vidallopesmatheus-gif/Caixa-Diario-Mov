import { type ReactNode } from 'react'

interface ModalProps {
  open: boolean
  title: string
  onClose: () => void
  children: ReactNode
  footer?: ReactNode
}
export default function Modal({ open, title, onClose, children, footer }: ModalProps) {
  if (!open) return null
  return (
    <div className="modal-overlay open" onClick={e => { if (e.target === e.currentTarget) onClose() }}>
      <div className="modal">
        <div className="modal-title">{title}</div>
        {children}
        {footer && <div className="modal-footer">{footer}</div>}
      </div>
    </div>
  )
}
