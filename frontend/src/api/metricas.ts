import { apiFetch } from './client'
import type { ApiResponse } from '../types'

export interface EbitdaMetrica {
  valor: number
  percentual?: number
  semaforo: string
}

export interface PrimeCostMetrica {
  percentual?: number
  semaforo: string
}

export interface PontoDeEquilibrioMetrica {
  valor: number
  receita: number
  semaforo: string
}

export interface ValuationMetrica {
  valor: number
  semaforo: string
}

export interface RunwayMetrica {
  meses: number
  semaforo: string
}

export interface LiquidezMetrica {
  indice?: number
  altaLiquidez: boolean
  semaforo: string
}

export interface MetricasPeriodo {
  ebitda?: EbitdaMetrica
  primeCost?: PrimeCostMetrica
  pontoDeEquilibrio?: PontoDeEquilibrioMetrica
  saldoProjetado: number
  valuation?: ValuationMetrica
  runway?: RunwayMetrica
  liquidez?: LiquidezMetrica
}

export interface EvolucaoMensal {
  mes: string
  receita: number
  custos: number
  lucro: number
  saldo: number
}

export interface FluxoDia {
  data: string
  saldoProjetado: number
}

export interface FluxoProjetado {
  saldoAtual: number
  dias: FluxoDia[]
}

export async function obterMetricas(clienteId: string, de: string, ate: string): Promise<MetricasPeriodo> {
  const res = await apiFetch<ApiResponse<MetricasPeriodo>>(`/api/metricas/${clienteId}?de=${de}&ate=${ate}`)
  return res.dados
}

export async function obterEvolucao(clienteId: string, meses = 12): Promise<EvolucaoMensal[]> {
  const res = await apiFetch<ApiResponse<EvolucaoMensal[]>>(`/api/metricas/${clienteId}/evolucao?meses=${meses}`)
  return res.dados
}

export async function obterFluxoProjetado(clienteId: string, dias = 90): Promise<FluxoProjetado> {
  const res = await apiFetch<ApiResponse<FluxoProjetado>>(`/api/metricas/${clienteId}/fluxo-projetado?dias=${dias}`)
  return res.dados
}
