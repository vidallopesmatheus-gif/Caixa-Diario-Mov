import { useEffect, useRef, useState } from 'react'

/** Checagem compartilhada de prefers-reduced-motion — usada tanto pelo count-up quanto pra
 * desligar a animação de entrada dos gráficos Recharts (`isAnimationActive`). */
export const prefereMovimentoReduzido = () =>
  typeof window !== 'undefined' && !!window.matchMedia?.('(prefers-reduced-motion: reduce)').matches

/** Anima um número de 0 (ou do valor anterior) até `valor` em `duracaoMs` — usado no MvHero.
 * Respeita prefers-reduced-motion (pula direto pro valor final, sem animar). */
export function useCountUp(valor: number | null, duracaoMs = 600): number | null {
  const [exibido, setExibido] = useState(valor)
  const [ultimoSemAnimacao, setUltimoSemAnimacao] = useState(valor)
  const anterior = useRef(valor)
  const semAnimacao = valor === null || prefereMovimentoReduzido()

  // Casos sem animação (valor nulo ou prefers-reduced-motion) são ajustados durante a renderização,
  // não num efeito — evita o passo intermediário "antigo → novo" que um efeito introduziria.
  if (semAnimacao && valor !== ultimoSemAnimacao) {
    setUltimoSemAnimacao(valor)
    setExibido(valor)
  }

  useEffect(() => {
    if (valor === null || prefereMovimentoReduzido()) { anterior.current = valor; return }

    const de = anterior.current ?? 0
    const para = valor
    const inicio = performance.now()
    let frame: number

    function passo(agora: number) {
      const t = Math.min(1, (agora - inicio) / duracaoMs)
      // ease-out cúbico — desacelera suavemente no final, sem "bater" no valor.
      const facil = 1 - Math.pow(1 - t, 3)
      setExibido(de + (para - de) * facil)
      if (t < 1) frame = requestAnimationFrame(passo)
      else anterior.current = para
    }
    frame = requestAnimationFrame(passo)
    return () => cancelAnimationFrame(frame)
  }, [valor, duracaoMs])

  return exibido
}
