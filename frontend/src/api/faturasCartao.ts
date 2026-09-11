import { apiFetch } from './client'
import type { ApiResponse } from '../types'

export interface FaturaCartao {
  competencia: string // "yyyy-MM"
  dataFechamento: string
  dataVencimento: string
  valorTotal: number
  valorPago: number
  saldoDevedor: number
  quantidadeCompras: number
  status: 'Aberta' | 'Fechada' | 'Paga'
}

export interface PagamentoFatura {
  id: string
  contaCartaoId: string
  contaCartaoNome: string
  contaOrigemId: string
  contaOrigemNome: string
  competencia: string
  valorPago: number
  data: string
  criadoEm: string
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function mapFatura(raw: any): FaturaCartao {
  return {
    competencia: raw.competencia,
    dataFechamento: raw.dataFechamento,
    dataVencimento: raw.dataVencimento,
    valorTotal: raw.valorTotal ?? 0,
    valorPago: raw.valorPago ?? 0,
    saldoDevedor: raw.saldoDevedor ?? 0,
    quantidadeCompras: raw.quantidadeCompras ?? 0,
    status: raw.status,
  }
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function mapPagamento(raw: any): PagamentoFatura {
  return {
    id: raw.id,
    contaCartaoId: raw.contaCartaoId,
    contaCartaoNome: raw.contaCartaoNome ?? '',
    contaOrigemId: raw.contaOrigemId,
    contaOrigemNome: raw.contaOrigemNome ?? '',
    competencia: raw.competencia,
    valorPago: raw.valorPago ?? 0,
    data: raw.data,
    criadoEm: raw.criadoEm,
  }
}

export const listarFaturasCartao = async (contaCartaoId: string): Promise<FaturaCartao[]> => {
  const res = await apiFetch<ApiResponse<unknown[]>>(`/api/faturas-cartao/${contaCartaoId}`)
  return (res.dados ?? []).map(mapFatura)
}

export const sugerirFaturaCartao = async (
  contaCartaoId: string, valor: number, data: string,
): Promise<FaturaCartao | null> => {
  const params = new URLSearchParams({ valor: String(valor), data })
  const res = await apiFetch<ApiResponse<unknown | null>>(`/api/faturas-cartao/${contaCartaoId}/sugestao?${params}`)
  return res.dados ? mapFatura(res.dados) : null
}

/**
 * Reclassifica uma saída já existente na conta corrente como pagamento de uma fatura de cartão —
 * o valor pago é sempre o do próprio lançamento, nunca informado à parte.
 */
export const vincularPagamentoFatura = async (dto: {
  contaOrigemId: string
  lancamentoId: string
  data: string
  contaCartaoId: string
  competencia: string
}): Promise<PagamentoFatura> => {
  const res = await apiFetch<ApiResponse<unknown>>('/api/faturas-cartao/vincular-pagamento', {
    method: 'POST',
    body: JSON.stringify({
      ContaOrigemId: dto.contaOrigemId,
      LancamentoId: dto.lancamentoId,
      Data: dto.data,
      ContaCartaoId: dto.contaCartaoId,
      Competencia: dto.competencia,
    }),
  })
  return mapPagamento(res.dados)
}

export const desvincularPagamentoFatura = async (id: string): Promise<void> => {
  await apiFetch<ApiResponse<null>>(`/api/faturas-cartao/${id}`, { method: 'DELETE' })
}
