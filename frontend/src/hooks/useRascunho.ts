import { useEffect, useRef } from 'react'
import { descartarRascunho, registrarRascunho } from '../utils/rascunhos'

/**
 * Money Vision ("desloga toda hora"): registra o estado atual do formulário pra ser salvo se a
 * sessão expirar nesta tela, e descarta o rascunho que a tela já aplicou ao abrir (lido com
 * lerRascunho no inicializador dos useState). `estado` null = nada digitado, nada a guardar.
 */
export function useRascunho<T>(chave: string, estado: T | null): void {
  const estadoRef = useRef(estado)

  useEffect(() => { estadoRef.current = estado })
  useEffect(() => registrarRascunho(chave, () => estadoRef.current), [chave])
  useEffect(() => { descartarRascunho(chave) }, [chave])
}
