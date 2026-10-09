import { apiFetch } from './client'
import type { ApiResponse } from '../types'

export interface SugestaoVinculo {
  contaProvisionadaId: string
  tipo: 'Receber' | 'Pagar'
  descricao: string
  valor: number
  dataVencimento?: string
  contaBancariaId: string
  lancamentoId: string
  lancamentoDescricao: string
  lancamentoValor: number
  lancamentoData: string
  score: number
}

// Fase 1.2: motor de sugestão de vínculo — compara títulos pendentes com lançamentos já
// importados no extrato (mesma conta, data/valor/descrição próximos) pra evitar baixa manual
// duplicando um dinheiro que já apareceu. Sem cache: a tela decide quando re-buscar.
export async function listarSugestoesVinculo(
  clienteId: string, de: string, ate: string, contaBancariaId?: string,
): Promise<SugestaoVinculo[]> {
  const params = new URLSearchParams({ de, ate })
  if (contaBancariaId) params.set('contaBancariaId', contaBancariaId)
  const res = await apiFetch<ApiResponse<SugestaoVinculo[]>>(`/api/conciliacao/${clienteId}/sugestoes?${params}`)
  return res.dados ?? []
}

// Item 3.3: "Ignorar" grava a decisão — esse par título×lançamento não é sugerido de novo.
export async function ignorarSugestaoVinculo(
  clienteId: string, contaProvisionadaId: string, lancamentoId: string,
): Promise<void> {
  await apiFetch<ApiResponse<null>>(
    `/api/conciliacao/${clienteId}/sugestoes/ignorar`,
    { method: 'POST', body: JSON.stringify({ contaProvisionadaId, lancamentoId }) },
  )
}
