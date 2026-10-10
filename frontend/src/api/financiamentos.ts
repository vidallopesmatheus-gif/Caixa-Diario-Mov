import { apiFetch } from './client'
import type { ApiResponse, Financiamento, ParcelaFinanciamento } from '../types'

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function mapFinanciamento(raw: any): Financiamento {
  return {
    id: raw.id, descricao: raw.descricao, tipo: raw.tipo, valorBem: raw.valorBem,
    valorEntrada: raw.valorEntrada, valorFinanciado: raw.valorFinanciado,
    numeroParcelas: raw.numeroParcelas, valorParcela: raw.valorParcela ?? undefined,
    taxaJurosMensal: raw.taxaJurosMensal ?? undefined, sistema: raw.sistema,
    dataPrimeiraParcela: raw.dataPrimeiraParcela, contaBancariaId: raw.contaBancariaId,
    contaBancariaNome: raw.contaBancariaNome, bemVinculado: raw.bemVinculado ?? undefined,
    status: raw.status, saldoDevedor: raw.saldoDevedor, totalParcelasAtual: raw.totalParcelasAtual,
    parcelasPagas: raw.parcelasPagas, percentualParcelasPagas: raw.percentualParcelasPagas,
    percentualDividaQuitada: raw.percentualDividaQuitada, jurosPagos: raw.jurosPagos,
    jurosAPagar: raw.jurosAPagar, jurosTotais: raw.jurosTotais,
    proximaParcelaValor: raw.proximaParcelaValor ?? undefined,
    proximaParcelaVencimento: raw.proximaParcelaVencimento ?? undefined,
    dataPrevistaQuitacao: raw.dataPrevistaQuitacao ?? undefined,
    valorAdiantado: raw.valorAdiantado, economiaJurosAcumulada: raw.economiaJurosAcumulada,
  }
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function mapParcela(raw: any): ParcelaFinanciamento {
  return {
    id: raw.id, numero: raw.numero, vencimento: raw.vencimento, valor: raw.valor,
    juros: raw.juros, amortizacao: raw.amortizacao, encargos: raw.encargos,
    saldoDevedor: raw.saldoDevedor, pago: raw.pago, dataBaixa: raw.dataBaixa ?? undefined,
    atrasada: raw.atrasada,
  }
}

export const listarFinanciamentos = async (clienteId: string): Promise<Financiamento[]> => {
  const res = await apiFetch<ApiResponse<unknown[]>>(`/api/financiamentos/${clienteId}`)
  return (res.dados ?? []).map(mapFinanciamento)
}

export const criarFinanciamento = async (clienteId: string, dto: {
  descricao: string
  tipo: 'Financiamento' | 'Emprestimo' | 'Consorcio' | 'Parcelamento'
  valorBem: number
  valorEntrada: number
  numeroParcelas: number
  valorParcela?: number
  taxaJurosMensal?: number
  sistema: 'Price' | 'SAC' | 'SemJuros'
  dataPrimeiraParcela: string
  contaBancariaId: string
  bemVinculado?: string
  origemMetaId?: string
}): Promise<Financiamento> => {
  const res = await apiFetch<ApiResponse<unknown>>(`/api/financiamentos/${clienteId}`, {
    method: 'POST',
    body: JSON.stringify({
      Descricao: dto.descricao, Tipo: dto.tipo, ValorBem: dto.valorBem, ValorEntrada: dto.valorEntrada,
      NumeroParcelas: dto.numeroParcelas, ValorParcela: dto.valorParcela, TaxaJurosMensal: dto.taxaJurosMensal,
      Sistema: dto.sistema, DataPrimeiraParcela: dto.dataPrimeiraParcela, ContaBancariaId: dto.contaBancariaId,
      OrigemMetaId: dto.origemMetaId,
      BemVinculado: dto.bemVinculado,
    }),
  })
  return mapFinanciamento(res.dados)
}

export const listarParcelasFinanciamento = async (financiamentoId: string): Promise<ParcelaFinanciamento[]> => {
  const res = await apiFetch<ApiResponse<unknown[]>>(`/api/financiamentos/${financiamentoId}/parcelas`)
  return (res.dados ?? []).map(mapParcela)
}

export const baixarParcelaFinanciamento = async (
  financiamentoId: string, dto: { parcelaId: string; dataPagamento: string; valorPago?: number },
): Promise<ParcelaFinanciamento> => {
  const res = await apiFetch<ApiResponse<unknown>>(`/api/financiamentos/${financiamentoId}/parcelas/baixar`, {
    method: 'POST',
    body: JSON.stringify({ ParcelaId: dto.parcelaId, DataPagamento: dto.dataPagamento, ValorPago: dto.valorPago }),
  })
  return mapParcela(res.dados)
}

export const estornarParcelaFinanciamento = async (financiamentoId: string, parcelaId: string): Promise<void> => {
  await apiFetch<ApiResponse<null>>(`/api/financiamentos/${financiamentoId}/parcelas/${parcelaId}/estornar`, { method: 'POST' })
}

export interface AdiantamentoResultado {
  saldoAntes: number
  saldoDepois: number
  parcelasAntes: number
  parcelasDepois: number
  jurosRestantesAntes: number
  jurosRestantesDepois: number
  economiaJuros: number
}

export const adiantarFinanciamento = async (
  financiamentoId: string, dto: { valor: number; data: string; modo: 'ReduzirPrazo' | 'ReduzirParcela'; simular: boolean },
): Promise<AdiantamentoResultado> => {
  const res = await apiFetch<ApiResponse<AdiantamentoResultado>>(`/api/financiamentos/${financiamentoId}/adiantar`, {
    method: 'POST',
    body: JSON.stringify({ Valor: dto.valor, Data: dto.data, Modo: dto.modo, Simular: dto.simular }),
  })
  return res.dados
}

export const excluirFinanciamento = async (financiamentoId: string): Promise<void> => {
  await apiFetch<ApiResponse<null>>(`/api/financiamentos/${financiamentoId}`, { method: 'DELETE' })
}
