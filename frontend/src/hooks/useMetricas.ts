import { useState, useEffect, useCallback } from 'react'
import { obterMetricas } from '../api/metricas'
import type { MetricasPeriodo } from '../api/metricas'

export function useMetricas(clienteId: string | null, de: string, ate: string, multiplo = 3) {
  const [metricas, setMetricas] = useState<MetricasPeriodo | null>(null)
  const [loading, setLoading] = useState(false)

  const carregar = useCallback(async () => {
    if (!clienteId || !de || !ate) return
    setLoading(true)
    try {
      setMetricas(await obterMetricas(clienteId, de, ate, multiplo))
    } catch (e) {
      console.error(e)
    } finally {
      setLoading(false)
    }
  }, [clienteId, de, ate, multiplo])

  useEffect(() => { carregar() }, [carregar])

  return { metricas, loading }
}
