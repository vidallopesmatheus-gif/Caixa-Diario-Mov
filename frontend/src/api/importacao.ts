import { apiFetch } from './client'
import type { ApiResponse, TransacaoImportada } from '../types'

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function mapTransacao(raw: any): TransacaoImportada {
  return {
    id: raw.id,
    contaBancariaId: raw.contaBancariaId,
    data: raw.data,
    valor: raw.valor ?? 0,
    descricao: raw.descricao ?? '',
    tipo: raw.tipo,
    status: raw.status,
    categoria: raw.categoria ?? undefined,
    fitId: raw.fitId ?? undefined,
    duplicada: raw.duplicada ?? false,
  }
}

export const importarExtrato = async (
  contaId: string,
  arquivo: File,
): Promise<TransacaoImportada[]> => {
  const form = new FormData()
  form.append('arquivo', arquivo)
  const res = await apiFetch<ApiResponse<unknown[]>>(
    `/api/contas-bancarias/${contaId}/importar-extrato`,
    { method: 'POST', body: form },
  )
  return (res.dados ?? []).map(mapTransacao)
}

export const listarPendentes = async (contaId: string): Promise<TransacaoImportada[]> => {
  const res = await apiFetch<ApiResponse<unknown[]>>(
    `/api/contas-bancarias/${contaId}/transacoes-pendentes`,
  )
  return (res.dados ?? []).map(mapTransacao)
}

export const confirmarTransacoes = async (
  contaId: string,
  transacoes: Array<{ id: string; categoria?: string; ignorar: boolean }>,
): Promise<void> => {
  await apiFetch<ApiResponse<null>>(
    `/api/contas-bancarias/${contaId}/confirmar-transacoes`,
    { method: 'POST', body: JSON.stringify({ transacoes }) },
  )
}
