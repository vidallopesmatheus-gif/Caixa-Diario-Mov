import { useRegisterSW } from 'virtual:pwa-register/react'
import './PwaUpdateBanner.css'

// Item 2.8: registerType 'prompt' (vite.config.ts) não troca o service worker sozinho — só avisa
// via needRefresh, e quem decide quando trocar é o usuário clicando em "Atualizar". Antes, com
// 'autoUpdate', o SW novo só assumia numa aba já aberta na PRÓXIMA navegação/reload (senão fica
// esperando todas as abas fecharem), dando a impressão de que a versão nova "só aparece na 2ª visita".
export default function PwaUpdateBanner() {
  const {
    needRefresh: [needRefresh],
    updateServiceWorker,
  } = useRegisterSW({
    onRegisterError(error: unknown) {
      console.error('Erro ao registrar o service worker:', error)
    },
  })

  if (!needRefresh) return null

  return (
    <div className="pwa-update-banner" role="status" aria-live="polite">
      <span className="pwa-update-icon">⬆️</span>
      <span className="pwa-update-text">Nova versão disponível</span>
      <button className="pwa-update-btn" onClick={() => updateServiceWorker(true)}>Atualizar</button>
    </div>
  )
}
