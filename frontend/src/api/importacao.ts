import { apiFetch } from './client'
import type { ApiResponse, ResumoImportacao, ResultadoImportacao, PendenteCategorizacao } from '../types'

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function mapResumo(raw: any): ResumoImportacao {
  return {
    totalEncontradas: raw.totalEncontradas ?? 0,
    totalJaImportadas: raw.totalJaImportadas ?? 0,
    totalNovas: raw.totalNovas ?? 0,
    totalEntradas: raw.totalEntradas ?? 0,
    totalSaidas: raw.totalSaidas ?? 0,
    dataInicioArquivo: raw.dataInicioArquivo ?? '',
    dataFimArquivo: raw.dataFimArquivo ?? '',
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

export const previewExtrato = async (
  contaId: string,
  arquivo: File,
  opcoes?: { dataInicio?: string; dataFim?: string },
): Promise<ResumoImportacao> => {
  const form = new FormData()
  form.append('arquivo', arquivo)
  if (opcoes?.dataInicio) form.append('dataInicio', opcoes.dataInicio)
  if (opcoes?.dataFim) form.append('dataFim', opcoes.dataFim)
  const res = await apiFetch<ApiResponse<unknown>>(
    `/api/contas-bancarias/${contaId}/preview-extrato`,
    { method: 'POST', body: form },
  )
  return mapResumo(res.dados)
}

export const importarExtrato = async (
  contaId: string,
  arquivo: File,
  opcoes?: { dataInicio?: string; dataFim?: string },
): Promise<ResultadoImportacao> => {
  const form = new FormData()
  form.append('arquivo', arquivo)
  if (opcoes?.dataInicio) form.append('dataInicio', opcoes.dataInicio)
  if (opcoes?.dataFim) form.append('dataFim', opcoes.dataFim)

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
