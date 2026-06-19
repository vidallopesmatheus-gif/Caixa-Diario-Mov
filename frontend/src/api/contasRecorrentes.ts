import { apiFetch } from './client'
import type { ApiResponse, ContaRecorrente } from '../types'

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function mapContaRecorrente(raw: any): ContaRecorrente {
  return {
    id: raw.id,
    clienteId: raw.clienteId,
    descricao: raw.descricao,
    valor: raw.valor,
    categoria: raw.categoria,
    tipo: raw.tipo,
    dataInicio: raw.dataInicio,
    dataFim: raw.dataFim,
    periodicidade: raw.periodicidade,
    quantidadeParcelas: raw.quantidadeParcelas ?? undefined,
    ativo: raw.ativo,
    criadoEm: raw.criadoEm,
  }
}

export async function listarContasRecorrentes(clienteId: string): Promise<ContaRecorrente[]> {
  const res = await apiFetch<ApiResponse<unknown[]>>(`/api/contas-recorrentes/${clienteId}`)
  return (res.dados ?? []).map(mapContaRecorrente)
}

export async function criarContaRecorrente(dto: {
  clienteId: string
  descricao: string
  valor: number
  categoria?: string
  tipo: 'Receber' | 'Pagar'
  dataInicio: string
  dataFim?: string
  periodicidade: string
  quantidadeParcelas?: number
}): Promise<ContaRecorrente> {
  const res = await apiFetch<ApiResponse<unknown>>('/api/contas-recorrentes', {
    method: 'POST',
    body: JSON.stringify({
      clienteId: dto.clienteId,
      descricao: dto.descricao,
      valor: dto.valor,
      categoria: dto.categoria,
      tipo: dto.tipo,
      dataInicio: dto.dataInicio,
      dataFim: dto.dataFim,
      periodicidade: dto.periodicidade,
      quantidadeParcelas: dto.quantidadeParcelas,
    }),
  })
  return mapContaRecorrente(res.dados)
}

export async function desativarContaRecorrente(clienteId: string, id: string): Promise<void> {
  await apiFetch(`/api/contas-recorrentes/${clienteId}/${id}`, { method: 'DELETE' })
}
