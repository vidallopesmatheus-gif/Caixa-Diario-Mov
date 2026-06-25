import { useState, useEffect } from 'react'
import './InstallPrompt.css'

// Evento customizado do navegador para prompt de instalação PWA
interface BeforeInstallPromptEvent extends Event {
  prompt: () => Promise<void>
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>
}

export default function InstallPrompt() {
  const [promptEvent, setPromptEvent] = useState<BeforeInstallPromptEvent | null>(null)
  const [dismissed, setDismissed] = useState(
    () => sessionStorage.getItem('pwa-install-dismissed') === '1'
  )

  useEffect(() => {
    const handler = (e: Event) => {
      e.preventDefault()
      setPromptEvent(e as BeforeInstallPromptEvent)
    }
    window.addEventListener('beforeinstallprompt', handler)
    return () => window.removeEventListener('beforeinstallprompt', handler)
  }, [])

  // Esconde se o app já está instalado (modo standalone)
  if (window.matchMedia('(display-mode: standalone)').matches) return null
  if (!promptEvent || dismissed) return null

  const handleInstall = async () => {
    await promptEvent.prompt()
    const { outcome } = await promptEvent.userChoice
    if (outcome === 'accepted' || outcome === 'dismissed') {
      setPromptEvent(null)
    }
  }

  const handleDismiss = () => {
    setDismissed(true)
    sessionStorage.setItem('pwa-install-dismissed', '1')
  }

  return (
    <div className="install-banner" role="status" aria-live="polite">
      <span className="install-icon">📲</span>
      <span className="install-text">Instale o Caixa Diário para acesso rápido</span>
      <button className="install-btn" onClick={handleInstall}>Instalar</button>
      <button className="install-dismiss" onClick={handleDismiss} aria-label="Fechar">✕</button>
    </div>
  )
}
