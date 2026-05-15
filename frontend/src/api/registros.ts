import { apiFetch } from './client'
import type { ApiResponse, Registro, Saida, ContaProvisionada } from '../types'

export const listarRegistros = (clienteId: string) =>
  apiFetch<ApiResponse<Registro[]>>(`/api/registros/${clienteId}`)

export const obterRegistroPorData = (clienteId: string, data: string) =>
  apiFetch<ApiResponse<Registro>>(`/api/registros/${clienteId}/${data}`)

export const salvarRegistro = (dto: {
  clienteId: string
  data: string
  saldoInicio: number
  entrada: number
  saidas: Saida[]
  contasAReceber: ContaProvisionada[]
  contasAPagar: ContaProvisionada[]
  saldoConfirmado: number
}) =>
  apiFetch<ApiResponse<Registro>>('/api/registros', {
    method: 'POST',
    body: JSON.stringify(dto),
  })

export const excluirRegistro = (
  clienteId: string,
  data: string,
  motivoExclusao: string
) =>
  apiFetch<ApiResponse<null>>(`/api/registros/${clienteId}/${data}`, {
    method: 'DELETE',
    body: JSON.stringify({ motivoExclusao }),
  })
