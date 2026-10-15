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

// Cartão (Bloco 2): bancos já usados por este cliente — alimenta o combobox editável da
// importação ("Nubank, C6, Itaú..."; digitar um nome novo "cria" o emissor, sem cadastro à parte).
export const listarBancosCartao = async (contaCartaoId: string): Promise<string[]> => {
  const res = await apiFetch<ApiResponse<string[]>>(`/api/faturas-cartao/${contaCartaoId}/bancos`)
  return res.dados ?? []
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

/**
 * Item F4: paga uma fatura direto da tela de Faturas, sem precisar de um lançamento já existente
 * (diferente de vincularPagamentoFatura) — cria a saída na conta de origem e a entrada no cartão
 * do zero. Se valor for omitido, paga o saldo devedor atual da fatura (pagamento integral).
 */
export const pagarFatura = async (contaCartaoId: string, dto: {
  contaOrigemId: string
  data: string
  competencia: string
  valor?: number
}): Promise<PagamentoFatura> => {
  const res = await apiFetch<ApiResponse<unknown>>(`/api/faturas-cartao/${contaCartaoId}/pagar`, {
    method: 'POST',
    body: JSON.stringify({
      ContaOrigemId: dto.contaOrigemId, Data: dto.data, Competencia: dto.competencia, Valor: dto.valor,
    }),
  })
  return mapPagamento(res.dados)
}

/**
 * Item 9: lança uma compra parcelada direto no cartão — cria uma saída por parcela, uma em cada
 * competência futura (um mês depois da anterior), nunca um único lançamento "informativo".
 */
export const lancarCompraParcelada = async (contaCartaoId: string, dto: {
  descricao: string
  valorTotal: number
  categoria: string
  quantidadeParcelas: number
  data: string
}): Promise<void> => {
  await apiFetch<ApiResponse<null>>(`/api/faturas-cartao/${contaCartaoId}/lancar-parcelado`, {
    method: 'POST',
    body: JSON.stringify({
      Descricao: dto.descricao,
      ValorTotal: dto.valorTotal,
      Categoria: dto.categoria,
      QuantidadeParcelas: dto.quantidadeParcelas,
      Data: dto.data,
    }),
  })
}
