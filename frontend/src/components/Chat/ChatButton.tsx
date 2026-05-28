import './Chat.css'

interface ChatButtonProps {
  onClick: () => void
}

export default function ChatButton({ onClick }: ChatButtonProps) {
  return (
    <button className="chat-button" onClick={onClick} aria-label="Abrir assistente">
      💬
    </button>
  )
}
