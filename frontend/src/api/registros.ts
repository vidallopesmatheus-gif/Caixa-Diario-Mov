import { apiFetch } from './client'
import type { ApiResponse, Registro, ItemFinanceiro, ItemFinanceiroSaida, ContaProvisionada } from '../types'

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function mapContaProvisionada(raw: any): ContaProvisionada {
  return {
    descricao: raw.Descricao ?? raw.descricao ?? '',
    valor: raw.Valor ?? raw.valor ?? 0,
    dataVencimento: raw.DataVencimento ?? raw.dataVencimento,
    pago: raw.Pago ?? raw.pago ?? false,
    categoria: raw.Categoria ?? raw.categoria,
    recorrenciaId: raw.RecorrenciaId ?? raw.recorrenciaId,
    dataBaixa: raw.DataBaixa ?? raw.dataBaixa,
    contaBancariaId: raw.ContaBancariaId ?? raw.contaBancariaId,
    lancamentoVinculadoId: raw.LancamentoVinculadoId ?? raw.lancamentoVinculadoId ?? undefined,
  }
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function mapItemFinanceiro(raw: any): ItemFinanceiro {
  return {
    id: raw.Id ?? raw.id ?? undefined,
    descricao: raw.Descricao ?? raw.descricao ?? '',
    valor: raw.Valor ?? raw.valor ?? 0,
    categoria: raw.Categoria ?? raw.categoria,
    tipoCusto: raw.TipoCusto ?? raw.tipoCusto,
    transferenciaId: raw.TransferenciaId ?? raw.transferenciaId ?? undefined,
    fitId: raw.FitId ?? raw.fitId ?? undefined,
    pendenteCategorizacao: raw.PendenteCategorizacao ?? raw.pendenteCategorizacao ?? false,
  }
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function mapItemFinanceiroSaida(raw: any): ItemFinanceiroSaida {
  return {
    id: raw.Id ?? raw.id ?? undefined,
    descricao: raw.Descricao ?? raw.descricao ?? '',
    valor: raw.Valor ?? raw.valor ?? 0,
    categoria: raw.Categoria ?? raw.categoria ?? 'Administrativas',
    subcategoria: raw.Subcategoria ?? raw.subcategoria ?? '',
    tipoCusto: raw.TipoCusto ?? raw.tipoCusto,
    transferenciaId: raw.TransferenciaId ?? raw.transferenciaId ?? undefined,
    fitId: raw.FitId ?? raw.fitId ?? undefined,
    pendenteCategorizacao: raw.PendenteCategorizacao ?? raw.pendenteCategorizacao ?? false,
  }
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function mapRegistro(raw: any): Registro {
  return {
    id: raw.id,
    clienteId: raw.clienteId,
    contaBancariaId: raw.contaBancariaId ?? undefined,
    data: raw.data,
    saldoInicio: raw.inicio ?? 0,
    entradas: (raw.entradas ?? []).map(mapItemFinanceiro),
    saidas: (raw.saidas ?? []).map(mapItemFinanceiroSaida),
    contasAReceber: (raw.contasReceber ?? []).map(mapContaProvisionada),
    contasAPagar: (raw.contasPagar ?? []).map(mapContaProvisionada),
    saldoConfirmado: raw.saldoFinal ?? 0,
    saldoCalculado: raw.saldoCalculado ?? 0,
    criadoEm: raw.salvoEm ?? '',
  }
}

export const listarRegistros = async (clienteId: string): Promise<ApiResponse<Registro[]>> => {
  const res = await apiFetch<ApiResponse<unknown[]>>(`/api/registros/${clienteId}`)
  return { ...res, dados: (res.dados ?? []).map(mapRegistro) }
}

export const obterRegistroPorData = async (clienteId: string, data: string, contaBancariaId?: string): Promise<ApiResponse<Registro>> => {
  const query = contaBancariaId ? `?contaBancariaId=${contaBancariaId}` : ''
  const res = await apiFetch<ApiResponse<unknown>>(`/api/registros/${clienteId}/${data}${query}`)
  return { ...res, dados: res.dados ? mapRegistro(res.dados) : res.dados as Registro }
}

export const salvarRegistro = async (dto: {
  clienteId: string
  contaBancariaId?: string
  data: string
  saldoInicio: number
  entradas: ItemFinanceiro[]
  saidas: ItemFinanceiroSaida[]
  contasAReceber: ContaProvisionada[]
  contasAPagar: ContaProvisionada[]
  saldoConfirmado: number
}): Promise<ApiResponse<Registro>> => {
  const payload = {
    clienteId: dto.clienteId,
    contaBancariaId: dto.contaBancariaId ?? null,
    data: dto.data,
    inicio: dto.saldoInicio,
    entradas: dto.entradas.map(e => ({
      Id: e.id, Descricao: e.descricao, Valor: e.valor, Categoria: e.categoria, TipoCusto: e.tipoCusto,
      TransferenciaId: e.transferenciaId, FitId: e.fitId, PendenteCategorizacao: e.pendenteCategorizacao,
    })),
    saidas: dto.saidas.map(s => ({
      Id: s.id,
      Descricao: s.descricao,
      Valor: s.valor,
      Categoria: s.categoria,
      Subcategoria: s.subcategoria || undefined,
      TipoCusto: s.tipoCusto,
      TransferenciaId: s.transferenciaId,
      FitId: s.fitId,
      PendenteCategorizacao: s.pendenteCategorizacao,
    })),
    contasReceber: dto.contasAReceber.map(c => ({ Descricao: c.descricao, Valor: c.valor, DataVencimento: c.dataVencimento, Pago: c.pago, Categoria: c.categoria, RecorrenciaId: c.recorrenciaId, DataBaixa: c.dataBaixa, ContaBancariaId: c.contaBancariaId, LancamentoVinculadoId: c.lancamentoVinculadoId })),
    contasPagar: dto.contasAPagar.map(c => ({ Descricao: c.descricao, Valor: c.valor, DataVencimento: c.dataVencimento, Pago: c.pago, Categoria: c.categoria, RecorrenciaId: c.recorrenciaId, DataBaixa: c.dataBaixa, ContaBancariaId: c.contaBancariaId, LancamentoVinculadoId: c.lancamentoVinculadoId })),
    saldoFinal: dto.saldoConfirmado,
  }
  const res = await apiFetch<ApiResponse<unknown>>('/api/registros', {
    method: 'POST',
    body: JSON.stringify(payload),
  })
  return { ...res, dados: res.dados ? mapRegistro(res.dados) : res.dados as Registro }
}

export const excluirRegistro = (clienteId: string, data: string, motivoExclusao: string) =>
  apiFetch<ApiResponse<null>>(`/api/registros/${clienteId}/${data}`, {
    method: 'DELETE',
    body: JSON.stringify({ motivoExclusao }),
  })
