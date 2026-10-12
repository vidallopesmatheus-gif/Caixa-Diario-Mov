import { apiFetch } from './client'
import type { ApiResponse } from '../types'

export interface ProjecaoItem {
  descricao: string
  valor: number
  categoria?: string
  origem: 'Provisionado' | 'Recorrente' | 'Atrasado' | 'Estimativa' | 'Fatura'
}

export interface ProjecaoDia {
  data: string
  saldoInicio: number
  entradas: ProjecaoItem[]
  saidas: ProjecaoItem[]
  totalEntradas: number
  totalSaidas: number
  saldoFim: number
  saldoNegativo: boolean
}

export interface Projecao {
  saldoAtual: number
  totalDias: number
  dias: ProjecaoDia[]
}

// Bloco 2 da rodada "Visão de Futuro" — opções combináveis com qualquer janela (30/60/90 dias ou
// trajetória): "Provavel" segue as médias históricas sem ajuste; "Conservador" aplica uma margem
// de segurança (recebimentos estimados reduzidos, despesas estimadas aumentadas). incluirReserva
// soma de volta as contas tipo Investimento (de fora por padrão, mesma regra do Dashboard).
export type Cenario = 'Provavel' | 'Conservador'
export interface OpcoesProjecao {
  contaBancariaId?: string
  cenario?: Cenario
  incluirReserva?: boolean
}

function montarParams(opcoes?: OpcoesProjecao): URLSearchParams {
  const params = new URLSearchParams()
  if (opcoes?.contaBancariaId) params.set('contaBancariaId', opcoes.contaBancariaId)
  if (opcoes?.cenario) params.set('cenario', opcoes.cenario)
  if (opcoes?.incluirReserva) params.set('incluirReserva', 'true')
  return params
}

export async function obterProjecao(
  clienteId: string,
  dias: 30 | 60 | 90,
  opcoes?: OpcoesProjecao,
): Promise<Projecao> {
  const params = montarParams(opcoes)
  params.set('dias', String(dias))
  const res = await apiFetch<ApiResponse<Projecao>>(
    `/api/projecao/${clienteId}?${params}`,
  )
  return res.dados
}

export interface TrajetoriaPonto {
  mes: string // "yyyy-MM"
  saldo: number
}

export interface Trajetoria {
  historico: TrajetoriaPonto[]
  projetado: TrajetoriaPonto[]
  mesesHistoricoDisponiveis: number
  saldoAtual: number
  variacaoRealizada: number
  variacaoProjetada: number
}

export async function obterTrajetoria(
  clienteId: string,
  mesesPassado = 6,
  mesesFuturo = 6,
  opcoes?: OpcoesProjecao,
): Promise<Trajetoria> {
  const params = montarParams(opcoes)
  params.set('mesesPassado', String(mesesPassado))
  params.set('mesesFuturo', String(mesesFuturo))
  const res = await apiFetch<ApiResponse<Trajetoria>>(
    `/api/projecao/${clienteId}/trajetoria?${params}`,
  )
  return res.dados
}

// Item 3 da rodada "Visão de Futuro": despesas fixas estimadas por categoria, não cadastradas
// como recorrência — cada uma pronta pra "Transformar em conta a pagar".
export interface DespesaEstimadaCategoria {
  categoria: string
  valorEstimado: number
}

export async function obterDespesasEstimadas(clienteId: string): Promise<DespesaEstimadaCategoria[]> {
  const res = await apiFetch<ApiResponse<DespesaEstimadaCategoria[]>>(
    `/api/projecao/${clienteId}/despesas-estimadas`,
  )
  return res.dados ?? []
}
