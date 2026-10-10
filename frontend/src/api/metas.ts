import { apiFetch } from './client'
import type { ApiResponse, MetaAnual } from '../types'

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function mapMeta(raw: any): MetaAnual {
  return {
    id: raw.id,
    clienteId: raw.clienteId,
    ano: raw.ano,
    metaReceita: raw.metaReceita,
    metaLucro: raw.metaLucro,
    mesInicio: raw.mesInicio ?? 1,
    periodoMeses: raw.periodoMeses ?? 12,
    salvoEm: raw.salvoEm ?? new Date().toISOString(),
    criadoEm: raw.criadoEm ?? raw.salvoEm ?? new Date().toISOString(),
    sonho: raw.sonho ?? '',
    modoMeta: raw.modoMeta === 'metodo' ? 'metodo' : 'simples',
    valorSonho: raw.valorSonho ?? 0,
    prazoAnos: raw.prazoAnos ?? 0,
    taxaRetorno: raw.taxaRetorno ?? 0,
    totalInvestido: raw.totalInvestido ?? 0,
    margemPJ: raw.margemPJ ?? undefined,
    iconeSonho: raw.iconeSonho ?? undefined,
    contaInvestimentoId: raw.contaInvestimentoId ?? undefined,
    dataAlvo: raw.dataAlvo ?? undefined,
    status: raw.status === 'Conquistada' || raw.status === 'Cancelada' ? raw.status : 'EmAndamento',
    dataConquista: raw.dataConquista ?? undefined,
    valorPago: raw.valorPago ?? undefined,
    formaPagamento: raw.formaPagamento ?? undefined,
    parcelas: raw.parcelas ?? undefined,
    dataPrimeiraParcela: raw.dataPrimeiraParcela ?? undefined,
    contaPagamentoId: raw.contaPagamentoId ?? undefined,
    categoria: raw.categoria ?? undefined,
    observacao: raw.observacao ?? undefined,
    transferenciaResgateId: raw.transferenciaResgateId ?? undefined,
    financiamentoId: raw.financiamentoId ?? undefined,
  }
}

export const obterMeta = async (clienteId: string, ano: number): Promise<MetaAnual | null> => {
  const res = await apiFetch<ApiResponse<unknown>>(`/api/metas/${clienteId}/${ano}`)
  return res.dados ? mapMeta(res.dados) : null
}

export const listarMetas = async (clienteId: string): Promise<MetaAnual[]> => {
  const res = await apiFetch<ApiResponse<unknown[]>>(`/api/metas/${clienteId}`)
  return (res.dados ?? []).map(mapMeta)
}

export const salvarMeta = async (dto: {
  id?: string
  clienteId: string
  ano: number
  metaReceita: number
  metaLucro: number
  mesInicio: number
  periodoMeses: number
  sonho?: string
  modoMeta?: string
  valorSonho?: number
  prazoAnos?: number
  taxaRetorno?: number
  totalInvestido?: number
  margemPJ?: number
  iconeSonho?: string
  dataAlvo?: string
}): Promise<MetaAnual> => {
  const res = await apiFetch<ApiResponse<unknown>>('/api/metas', {
    method: 'POST',
    body: JSON.stringify(dto),
  })
  return mapMeta(res.dados)
}

/** Remove um objetivo (modo "metodo"). A meta de faturamento mensal (modo "simples") não usa isso. */
export const excluirMeta = async (id: string): Promise<void> => {
  await apiFetch<ApiResponse<null>>(`/api/metas/${id}`, { method: 'DELETE' })
}

/** Fase 5: "🏆 Conquistei" — registra a compra de verdade por trás do objetivo. */
export const conquistarMeta = async (id: string, dto: {
  valorPago: number
  data: string
  formaPagamento: 'AVista' | 'Cartao' | 'Boleto' | 'Financiado'
  parcelas?: number
  dataPrimeiraParcela?: string
  contaPagamentoId: string
  categoria: string
  observacao?: string
  resgatarDoInvestimento?: boolean
}): Promise<MetaAnual> => {
  const res = await apiFetch<ApiResponse<unknown>>(`/api/metas/${id}/conquistar`, {
    method: 'POST',
    body: JSON.stringify({
      ValorPago: dto.valorPago, Data: dto.data, FormaPagamento: dto.formaPagamento,
      Parcelas: dto.parcelas, DataPrimeiraParcela: dto.dataPrimeiraParcela,
      ContaPagamentoId: dto.contaPagamentoId, Categoria: dto.categoria, Observacao: dto.observacao,
      ResgatarDoInvestimento: dto.resgatarDoInvestimento ?? false,
    }),
  })
  return mapMeta(res.dados)
}

export interface DesfazerConquistaResultado {
  removidos: string[]
  mantidos: string[]
}

export const desfazerConquistaMeta = async (id: string): Promise<DesfazerConquistaResultado> => {
  const res = await apiFetch<ApiResponse<{ removidos?: string[]; mantidos?: string[] }>>(
    `/api/metas/${id}/desfazer-conquista`, { method: 'POST' },
  )
  return { removidos: res.dados?.removidos ?? [], mantidos: res.dados?.mantidos ?? [] }
}
