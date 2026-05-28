import { useState, useCallback } from 'react'
import { enviarMensagem } from '../api/chat'
import type { ChatMessage } from '../types'

export function useChat() {
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [isLoading, setIsLoading] = useState(false)

  const sendMessage = useCallback(async (text: string) => {
    if (!text.trim() || isLoading) return

    const userMsg: ChatMessage = { role: 'user', content: text }
    setMessages(prev => [...prev, userMsg])
    setIsLoading(true)

    try {
      const res = await enviarMensagem(text, messages)
      const assistantMsg: ChatMessage = { role: 'assistant', content: res.dados.reply }
      setMessages(prev => [...prev, assistantMsg])
    } catch {
      const errMsg: ChatMessage = {
        role: 'assistant',
        content: 'Erro ao conectar com o assistente. Tente novamente.',
      }
      setMessages(prev => [...prev, errMsg])
    } finally {
      setIsLoading(false)
    }
  }, [messages, isLoading])

  const clearMessages = useCallback(() => setMessages([]), [])

  return { messages, isLoading, sendMessage, clearMessages }
}
