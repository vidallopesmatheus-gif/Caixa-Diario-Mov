import { apiFetch } from './client'
import type { ApiResponse, PreviewTransacao, ResultadoImportacao, PendenteCategorizacao } from '../types'

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function mapPreview(raw: any): PreviewTransacao {
  return {
    indice: raw.indice ?? 0,
    data: raw.data,
    valor: raw.valor ?? 0,
    descricao: raw.descricao ?? '',
    tipo: raw.tipo,
    fitId: raw.fitId ?? undefined,
    jaImportada: raw.jaImportada ?? false,
  }
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function mapPendente(raw: any): PendenteCategorizacao {
  return {
    id: raw.id,
    data: raw.data,
    descricao: raw.descricao ?? '',
    valor: raw.valor ?? 0,
    tipo: raw.tipo,
  }
}

export const previewExtrato = async (contaId: string, arquivo: File): Promise<PreviewTransacao[]> => {
  const form = new FormData()
  form.append('arquivo', arquivo)
  const res = await apiFetch<ApiResponse<{ transacoes: unknown[] }>>(
    `/api/contas-bancarias/${contaId}/preview-extrato`,
    { method: 'POST', body: form },
  )
  return (res.dados?.transacoes ?? []).map(mapPreview)
}

export const importarExtrato = async (
  contaId: string,
  arquivo: File,
  opcoes?: { dataInicio?: string; dataFim?: string; indicesForcarInclusao?: number[] },
): Promise<ResultadoImportacao> => {
  const form = new FormData()
  form.append('arquivo', arquivo)
  if (opcoes?.dataInicio) form.append('dataInicio', opcoes.dataInicio)
  if (opcoes?.dataFim) form.append('dataFim', opcoes.dataFim)
  for (const i of opcoes?.indicesForcarInclusao ?? []) form.append('indicesForcarInclusao', String(i))

  const res = await apiFetch<ApiResponse<unknown>>(
    `/api/contas-bancarias/${contaId}/importar-extrato`,
    { method: 'POST', body: form },
  )
  const d = res.dados as Record<string, unknown>
  return {
    totalImportadas: Number(d.totalImportadas ?? 0),
    totalPendentesCategorizacao: Number(d.totalPendentesCategorizacao ?? 0),
    totalEntradas: Number(d.totalEntradas ?? 0),
    totalSaidas: Number(d.totalSaidas ?? 0),
  }
}

export const listarPendentesCategorizacao = async (contaId: string): Promise<PendenteCategorizacao[]> => {
  const res = await apiFetch<ApiResponse<unknown[]>>(
    `/api/contas-bancarias/${contaId}/pendentes-categorizacao`,
  )
  return (res.dados ?? []).map(mapPendente)
}

export const categorizarPendentes = async (
  contaId: string,
  itens: Array<{ id: string; data: string; categoria: string }>,
): Promise<void> => {
  await apiFetch<ApiResponse<null>>(
    `/api/contas-bancarias/${contaId}/categorizar-pendentes`,
    { method: 'POST', body: JSON.stringify({ itens }) },
  )
}
