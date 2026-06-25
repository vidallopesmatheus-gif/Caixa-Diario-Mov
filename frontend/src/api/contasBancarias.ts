import { apiFetch } from './client'
import type { ApiResponse, ContaBancaria } from '../types'

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function mapConta(raw: any): ContaBancaria {
  return {
    id: raw.id,
    clienteId: raw.clienteId,
    nome: raw.nome,
    tipo: raw.tipo,
    saldoInicial: raw.saldoInicial ?? 0,
    saldoAtual: raw.saldoAtual ?? 0,
    ativa: raw.ativa ?? true,
    dataCriacao: raw.dataCriacao ?? '',
  }
}

export const listarContasBancarias = async (clienteId: string): Promise<ContaBancaria[]> => {
  const res = await apiFetch<ApiResponse<unknown[]>>(`/api/contas-bancarias/${clienteId}`)
  return (res.dados ?? []).map(mapConta)
}

export const criarContaBancaria = async (dto: {
  clienteId: string
  nome: string
  tipo: string
  saldoInicial: number
}): Promise<ContaBancaria> => {
  const res = await apiFetch<ApiResponse<unknown>>('/api/contas-bancarias', {
    method: 'POST',
    body: JSON.stringify(dto),
  })
  return mapConta(res.dados)
}

export const atualizarContaBancaria = async (id: string, dto: {
  nome: string
  tipo: string
  saldoInicial: number
  ativa: boolean
}): Promise<ContaBancaria> => {
  const res = await apiFetch<ApiResponse<unknown>>(`/api/contas-bancarias/${id}`, {
    method: 'PUT',
    body: JSON.stringify(dto),
  })
  return mapConta(res.dados)
}

export const inativarContaBancaria = async (id: string): Promise<void> => {
  await apiFetch<ApiResponse<null>>(`/api/contas-bancarias/${id}`, { method: 'DELETE' })
}
