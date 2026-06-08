import { useState, useRef, useEffect } from 'react'
import type { ChatMessage } from '../../types'
import './Chat.css'

interface ChatPanelProps {
  messages: ChatMessage[]
  isLoading: boolean
  onSend: (text: string) => void
  onClose: () => void
}

export default function ChatPanel({ messages, isLoading, onSend, onClose }: ChatPanelProps) {
  const [input, setInput] = useState('')
  const bottomRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages])

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    const text = input.trim()
    if (!text) return
    onSend(text)
    setInput('')
  }

  return (
    <div className="chat-panel">
      <div className="chat-panel__header">
        <span>Assistente Caixa Diário</span>
        <button className="chat-panel__close" onClick={onClose} aria-label="Fechar chat">✕</button>
      </div>

      <div className="chat-panel__messages">
        {messages.length === 0 && (
          <p className="chat-panel__empty">Como posso ajudar com o Caixa Diário?</p>
        )}
        {messages.map((msg, i) => (
          <div key={i} className={`chat-msg chat-msg--${msg.role}`}>
            {msg.content}
          </div>
        ))}
        {isLoading && (
          <div className="chat-msg chat-msg--assistant">...</div>
        )}
        <div ref={bottomRef} />
      </div>

      <form className="chat-panel__input-row" onSubmit={handleSubmit}>
        <input
          className="chat-panel__input"
          value={input}
          onChange={e => setInput(e.target.value)}
          placeholder="Digite sua pergunta..."
          disabled={isLoading}
          autoFocus
        />
        <button className="chat-panel__send" type="submit" disabled={isLoading || !input.trim()}>
          Enviar
        </button>
      </form>
    </div>
  )
}
