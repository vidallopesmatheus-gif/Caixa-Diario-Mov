import { apiFetch } from './client'
import type { ApiResponse } from '../types'

export interface Transferencia {
  id: string
  clienteId: string
  contaOrigemId: string
  contaOrigemNome: string
  contaDestinoId: string
  contaDestinoNome: string
  data: string
  valor: number
  descricao?: string
  criadoEm: string
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function mapTransferencia(raw: any): Transferencia {
  return {
    id: raw.id,
    clienteId: raw.clienteId,
    contaOrigemId: raw.contaOrigemId,
    contaOrigemNome: raw.contaOrigemNome ?? '',
    contaDestinoId: raw.contaDestinoId,
    contaDestinoNome: raw.contaDestinoNome ?? '',
    data: raw.data,
    valor: raw.valor ?? 0,
    descricao: raw.descricao ?? undefined,
    criadoEm: raw.criadoEm,
  }
}

export const criarTransferencia = async (dto: {
  clienteId: string
  contaOrigemId: string
  contaDestinoId: string
  data: string
  valor: number
  descricao?: string
}): Promise<Transferencia> => {
  const res = await apiFetch<ApiResponse<unknown>>('/api/transferencias', {
    method: 'POST',
    body: JSON.stringify(dto),
  })
  return mapTransferencia(res.dados)
}

export const listarTransferencias = async (clienteId: string): Promise<Transferencia[]> => {
  const res = await apiFetch<ApiResponse<unknown[]>>(`/api/transferencias/${clienteId}`)
  return (res.dados ?? []).map(mapTransferencia)
}

export const estornarTransferencia = async (id: string): Promise<void> => {
  await apiFetch<ApiResponse<null>>(`/api/transferencias/${id}`, { method: 'DELETE' })
}

/**
 * Reclassifica um lançamento já existente (ex.: "Aplicação RDB" importado como saída) como
 * Transferência — cria a perna contrapartida na conta informada, sem duplicar nem inflar o DRE.
 */
export const converterLancamentoEmTransferencia = async (dto: {
  contaId: string
  lancamentoId: string
  data: string
  tipo: 'Entrada' | 'Saida'
  contaContrapartidaId: string
  lancamentoContrapartidaId?: string
  dataContrapartida?: string
}): Promise<Transferencia> => {
  const res = await apiFetch<ApiResponse<unknown>>('/api/transferencias/converter-lancamento', {
    method: 'POST',
    body: JSON.stringify({
      ContaId: dto.contaId,
      LancamentoId: dto.lancamentoId,
      Data: dto.data,
      Tipo: dto.tipo,
      ContaContrapartidaId: dto.contaContrapartidaId,
      LancamentoContrapartidaId: dto.lancamentoContrapartidaId,
      DataContrapartida: dto.dataContrapartida,
    }),
  })
  return mapTransferencia(res.dados)
}
