import { useState, useEffect, useCallback } from 'react'
import { obterMetricas, obterFluxoProjetado } from '../api/metricas'
import type { MetricasPeriodo, FluxoProjetado } from '../api/metricas'

export function useMetricas(clienteId: string | null, de: string, ate: string, multiplo = 3) {
  const [metricas, setMetricas] = useState<MetricasPeriodo | null>(null)
  const [fluxo, setFluxo] = useState<FluxoProjetado | null>(null)
  const [loading, setLoading] = useState(false)

  const carregar = useCallback(async () => {
    if (!clienteId || !de || !ate) return
    setLoading(true)
    try {
      const [m, f] = await Promise.all([
        obterMetricas(clienteId, de, ate, multiplo),
        obterFluxoProjetado(clienteId, 30),
      ])
      setMetricas(m)
      setFluxo(f)
    } catch (e) {
      console.error(e)
    } finally {
      setLoading(false)
    }
  }, [clienteId, de, ate, multiplo])

  useEffect(() => { carregar() }, [carregar])

  return { metricas, fluxo, loading }
}
