import { useState } from 'react'
import type { ReactNode } from 'react'
import TopBar from './TopBar'
import TabsBar from './TabsBar'
import ChatButton from '../Chat/ChatButton'
import ChatPanel from '../Chat/ChatPanel'
import { useChat } from '../../hooks/useChat'
import './Layout.css'

export default function Layout({ children }: { children: ReactNode }) {
  const [chatOpen, setChatOpen] = useState(false)
  const { messages, isLoading, sendMessage, clearMessages } = useChat()

  const handleClose = () => {
    setChatOpen(false)
    clearMessages()
  }

  return (
    <>
      <TopBar />
      <TabsBar />
      <div className="tab-content">{children}</div>
      <ChatButton onClick={() => setChatOpen(o => !o)} />
      {chatOpen && (
        <ChatPanel
          messages={messages}
          isLoading={isLoading}
          onSend={sendMessage}
          onClose={handleClose}
        />
      )}
    </>
  )
}
