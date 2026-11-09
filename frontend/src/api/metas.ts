import { apiFetch } from './client'
import type { ApiResponse, MetaAnual, AndamentoConquista } from '../types'

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
    financiamentoStatus: raw.financiamentoStatus ?? undefined,
    parcelamentoIds: raw.parcelamentoIds ?? [],
    lancamentoIds: raw.lancamentoIds ?? [],
    andamento: raw.andamento ? mapAndamento(raw.andamento) : undefined,
  }
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function mapAndamento(raw: any): AndamentoConquista {
  return {
    valorPago: raw.valorPago ?? 0,
    valorTotal: raw.valorTotal ?? 0,
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    parcelamentos: (raw.parcelamentos ?? []).map((p: any) => ({
      id: p.id, descricao: p.descricao ?? '', banco: p.banco ?? undefined,
      parcelasPagas: p.parcelasPagas ?? 0, totalParcelas: p.totalParcelas ?? 0,
      valorParcela: p.valorParcela ?? 0, valorTotal: p.valorTotal ?? 0, dataUltimaParcela: p.dataUltimaParcela,
    })),
    dataTermino: raw.dataTermino ?? undefined,
    valorMensal: raw.valorMensal ?? 0,
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

export interface VinculoLancamentoMeta {
  lancamentoId: string
  contaBancariaId: string
  data: string
  /** Só pra lançamento solto que a detecção não reconheceu: "é a parcela N de M". */
  numeroParcela?: number
  totalParcelas?: number
}

/**
 * Fase 5: "🏆 Conquistei" — registra a compra de verdade por trás do objetivo. modo "JaLancado"
 * só vincula parcelamentos/lançamentos que já existem (nunca cria lançamento).
 */
export const conquistarMeta = async (id: string, dto: {
  modo?: 'LancarAgora' | 'JaLancado'
  valorPago?: number
  data: string
  formaPagamento: 'AVista' | 'Cartao' | 'Boleto' | 'Financiado'
  parcelas?: number
  dataPrimeiraParcela?: string
  contaPagamentoId?: string
  categoria?: string
  observacao?: string
  resgatarDoInvestimento?: boolean
  banco?: string
  valorParcela?: number
  parcelamentoIds?: string[]
  lancamentos?: VinculoLancamentoMeta[]
}): Promise<MetaAnual> => {
  const res = await apiFetch<ApiResponse<unknown>>(`/api/metas/${id}/conquistar`, {
    method: 'POST',
    body: JSON.stringify({
      Modo: dto.modo ?? 'LancarAgora',
      ValorPago: dto.valorPago ?? 0, Data: dto.data, FormaPagamento: dto.formaPagamento,
      Parcelas: dto.parcelas, DataPrimeiraParcela: dto.dataPrimeiraParcela,
      ContaPagamentoId: dto.contaPagamentoId || '00000000-0000-0000-0000-000000000000',
      Categoria: dto.categoria ?? '', Observacao: dto.observacao,
      ResgatarDoInvestimento: dto.resgatarDoInvestimento ?? false,
      Banco: dto.banco, ValorParcela: dto.valorParcela,
      ParcelamentoIds: dto.parcelamentoIds ?? [],
      Lancamentos: (dto.lancamentos ?? []).map(l => ({
        LancamentoId: l.lancamentoId, ContaBancariaId: l.contaBancariaId, Data: l.data,
        NumeroParcela: l.numeroParcela, TotalParcelas: l.totalParcelas,
      })),
    }),
  })
  return mapMeta(res.dados)
}

export interface ParcelamentoCandidato {
  id: string
  descricao: string
  banco?: string
  contaNome: string
  valorParcela: number
  totalParcelas: number
  parcelasPagas: number
  valorTotal: number
}

export interface LancamentoCandidato {
  id: string
  contaBancariaId: string
  contaNome: string
  contaEhCartao: boolean
  banco?: string
  data: string
  descricao: string
  valor: number
  categoria?: string
  parcelamentoId?: string
  numeroParcela?: number
  totalParcelas?: number
}

/** "Já lancei essa compra": parcelamentos detectados + saídas dos últimos 90 dias. */
export const listarCandidatosVinculoMeta = async (id: string): Promise<{
  parcelamentos: ParcelamentoCandidato[]
  lancamentos: LancamentoCandidato[]
}> => {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const res = await apiFetch<ApiResponse<{ parcelamentos?: any[]; lancamentos?: any[] }>>(`/api/metas/${id}/candidatos-vinculo`)
  return {
    parcelamentos: (res.dados?.parcelamentos ?? []).map(p => ({
      id: p.id, descricao: p.descricao ?? '', banco: p.banco ?? undefined, contaNome: p.contaNome ?? '',
      valorParcela: p.valorParcela ?? 0, totalParcelas: p.totalParcelas ?? 0,
      parcelasPagas: p.parcelasPagas ?? 0, valorTotal: p.valorTotal ?? 0,
    })),
    lancamentos: (res.dados?.lancamentos ?? []).map(l => ({
      id: l.id, contaBancariaId: l.contaBancariaId, contaNome: l.contaNome ?? '', contaEhCartao: !!l.contaEhCartao,
      banco: l.banco ?? undefined, data: String(l.data).slice(0, 10), descricao: l.descricao ?? '',
      valor: l.valor ?? 0, categoria: l.categoria ?? undefined, parcelamentoId: l.parcelamentoId ?? undefined,
      numeroParcela: l.numeroParcela ?? undefined, totalParcelas: l.totalParcelas ?? undefined,
    })),
  }
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
