import { apiFetch } from './client'
import type { ApiResponse, Registro, Saida, ContaProvisionada } from '../types'

// A API usa nomes de campos diferentes dos tipos internos. Este mapeamento
// centraliza a conversão para não poluir os componentes.
// eslint-disable-next-line @typescript-eslint/no-explicit-any
function mapRegistro(raw: any): Registro {
  return {
    id: raw.id,
    clienteId: raw.clienteId,
    data: raw.data,
    saldoInicio: raw.inicio ?? 0,
    entrada: raw.entrada ?? 0,
    saidas: raw.saidas ?? [],
    contasAReceber: raw.contasReceber ?? [],
    contasAPagar: raw.contasPagar ?? [],
    saldoConfirmado: raw.saldoFinal ?? 0,
    saldoCalculado: raw.saldoCalculado ?? 0,
    criadoEm: raw.salvoEm ?? '',
  }
}

export const listarRegistros = async (clienteId: string): Promise<ApiResponse<Registro[]>> => {
  const res = await apiFetch<ApiResponse<unknown[]>>(`/api/registros/${clienteId}`)
  return { ...res, dados: (res.dados ?? []).map(mapRegistro) }
}

export const obterRegistroPorData = async (clienteId: string, data: string): Promise<ApiResponse<Registro>> => {
  const res = await apiFetch<ApiResponse<unknown>>(`/api/registros/${clienteId}/${data}`)
  return { ...res, dados: res.dados ? mapRegistro(res.dados) : res.dados as Registro }
}

export const salvarRegistro = async (dto: {
  clienteId: string
  data: string
  saldoInicio: number
  entrada: number
  saidas: Saida[]
  contasAReceber: ContaProvisionada[]
  contasAPagar: ContaProvisionada[]
  saldoConfirmado: number
}): Promise<ApiResponse<Registro>> => {
  const payload = {
    clienteId: dto.clienteId,
    data: dto.data,
    inicio: dto.saldoInicio,
    entrada: dto.entrada,
    saidas: dto.saidas,
    contasReceber: dto.contasAReceber,
    contasPagar: dto.contasAPagar,
    saldoFinal: dto.saldoConfirmado,
  }
  const res = await apiFetch<ApiResponse<unknown>>('/api/registros', {
    method: 'POST',
    body: JSON.stringify(payload),
  })
  return { ...res, dados: res.dados ? mapRegistro(res.dados) : res.dados as Registro }
}

export const excluirRegistro = (
  clienteId: string,
  data: string,
  motivoExclusao: string
) =>
  apiFetch<ApiResponse<null>>(`/api/registros/${clienteId}/${data}`, {
    method: 'DELETE',
    body: JSON.stringify({ motivoExclusao }),
  })
