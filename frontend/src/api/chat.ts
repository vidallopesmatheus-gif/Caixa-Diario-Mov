import { apiFetch } from './client'
import type { ChatMessage, ChatResponse } from '../types'

export const enviarMensagem = (
  message: string,
  history: ChatMessage[]
): Promise<ChatResponse> =>
  apiFetch<ChatResponse>('/api/chat', {
    method: 'POST',
    body: JSON.stringify({ message, history }),
  })
