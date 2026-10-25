import { apiFetch } from './client'
import type {
  ApiResponse, ResumoImportacao, ResultadoImportacao, PendenteCategorizacao, DuplicataManual, DuplicataEntreArquivos,
} from '../types'

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function mapDuplicataManual(raw: any): DuplicataManual {
  return {
    transacaoIndice: raw.transacaoIndice,
    descricaoBanco: raw.descricaoBanco ?? '',
    dataBanco: raw.dataBanco ?? '',
    valor: raw.valor ?? 0,
    tipo: raw.tipo,
    lancamentoManualId: raw.lancamentoManualId,
    descricaoManual: raw.descricaoManual ?? '',
    dataManual: raw.dataManual ?? '',
  }
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function mapDuplicataEntreArquivos(raw: any): DuplicataEntreArquivos {
  return {
    transacaoIndice: raw.transacaoIndice,
    descricaoBanco: raw.descricaoBanco ?? '',
    dataBanco: raw.dataBanco ?? '',
    valor: raw.valor ?? 0,
    tipo: raw.tipo,
    descricaoJaImportada: raw.descricaoJaImportada ?? '',
    dataJaImportada: raw.dataJaImportada ?? '',
  }
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function mapResumo(raw: any): ResumoImportacao {
  return {
    totalEncontradas: raw.totalEncontradas ?? 0,
    totalJaImportadas: raw.totalJaImportadas ?? 0,
    totalNovas: raw.totalNovas ?? 0,
    totalEntradas: raw.totalEntradas ?? 0,
    totalSaidas: raw.totalSaidas ?? 0,
    quantidadeEntradas: raw.quantidadeEntradas ?? 0,
    quantidadeSaidas: raw.quantidadeSaidas ?? 0,
    sinalInvertidoParaCartao: raw.sinalInvertidoParaCartao ?? false,
    dataInicioArquivo: raw.dataInicioArquivo ?? '',
    dataFimArquivo: raw.dataFimArquivo ?? '',
    duplicatasManuais: (raw.duplicatasManuais ?? []).map(mapDuplicataManual),
    duplicatasEntreArquivos: (raw.duplicatasEntreArquivos ?? []).map(mapDuplicataEntreArquivos),
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
    categoria: raw.categoria ?? undefined,
    categoriaSugerida: raw.categoriaSugerida ?? false,
    sugestaoTransferenciaContaId: raw.sugestaoTransferenciaContaId ?? undefined,
    sugestaoTransferenciaContaNome: raw.sugestaoTransferenciaContaNome ?? undefined,
    sugestaoTransferenciaLancamentoId: raw.sugestaoTransferenciaLancamentoId ?? undefined,
    sugestaoTransferenciaData: raw.sugestaoTransferenciaData ?? undefined,
  }
}

export const previewExtrato = async (
  contaId: string,
  arquivo: File,
  opcoes?: { dataInicio?: string; dataFim?: string; inverterSinaisManualmente?: boolean },
): Promise<ResumoImportacao> => {
  const form = new FormData()
  form.append('arquivo', arquivo)
  if (opcoes?.dataInicio) form.append('dataInicio', opcoes.dataInicio)
  if (opcoes?.dataFim) form.append('dataFim', opcoes.dataFim)
  if (opcoes?.inverterSinaisManualmente !== undefined) {
    form.append('inverterSinaisManualmente', String(opcoes.inverterSinaisManualmente))
  }
  const res = await apiFetch<ApiResponse<unknown>>(
    `/api/contas-bancarias/${contaId}/preview-extrato`,
    { method: 'POST', body: form },
  )
  return mapResumo(res.dados)
}

export interface ResolucaoDuplicata {
  transacaoIndice: number
  // DuplicataManual: 'Mesclar' (padrão) | 'ImportarComoNovo'.
  // DuplicataEntreArquivos (item 3.1): 'Ignorar' (padrão) | 'Importar'.
  acao: 'Mesclar' | 'ImportarComoNovo' | 'Ignorar' | 'Importar'
}

export const importarExtrato = async (
  contaId: string,
  arquivo: File,
  opcoes?: {
    dataInicio?: string; dataFim?: string; resolucoesDuplicatas?: ResolucaoDuplicata[]
    // Cartão (Bloco 2): obrigatórios só quando a conta é CartaoCredito — ver ImportacaoService.
    banco?: string; dataVencimentoFatura?: string; dataFechamentoFatura?: string; competenciaFatura?: string
    // Mesmo valor usado no preview precisa ser reenviado aqui, senão a decisão de inverter ou não
    // o sinal é recalculada do zero (ver ImportacaoService.NormalizarSinalParaCartao).
    inverterSinaisManualmente?: boolean
  },
): Promise<ResultadoImportacao> => {
  const form = new FormData()
  form.append('arquivo', arquivo)
  if (opcoes?.dataInicio) form.append('dataInicio', opcoes.dataInicio)
  if (opcoes?.dataFim) form.append('dataFim', opcoes.dataFim)
  // Só precisa mandar as que o usuário trocou do padrão (Mesclar) — índice ausente já é Mesclar.
  if (opcoes?.resolucoesDuplicatas?.length) {
    form.append('resolucoesDuplicatasJson', JSON.stringify(opcoes.resolucoesDuplicatas))
  }
  if (opcoes?.banco) form.append('banco', opcoes.banco)
  if (opcoes?.dataVencimentoFatura) form.append('dataVencimentoFatura', opcoes.dataVencimentoFatura)
  if (opcoes?.dataFechamentoFatura) form.append('dataFechamentoFatura', opcoes.dataFechamentoFatura)
  if (opcoes?.competenciaFatura) form.append('competenciaFatura', opcoes.competenciaFatura)
  if (opcoes?.inverterSinaisManualmente !== undefined) {
    form.append('inverterSinaisManualmente', String(opcoes.inverterSinaisManualmente))
  }

  const res = await apiFetch<ApiResponse<unknown>>(
    `/api/contas-bancarias/${contaId}/importar-extrato`,
    { method: 'POST', body: form },
  )
  const d = res.dados as Record<string, unknown>
  return {
    totalImportadas: Number(d.totalImportadas ?? 0),
    totalPendentesCategorizacao: Number(d.totalPendentesCategorizacao ?? 0),
    totalCategorizadasPorRegra: Number(d.totalCategorizadasPorRegra ?? 0),
    totalConciliadasTransferencia: Number(d.totalConciliadasTransferencia ?? 0),
    totalAmbiguasTransferencia: Number(d.totalAmbiguasTransferencia ?? 0),
    totalEntradas: Number(d.totalEntradas ?? 0),
    totalSaidas: Number(d.totalSaidas ?? 0),
    totalMescladasComManual: Number(d.totalMescladasComManual ?? 0),
    totalDuplicatasEntreArquivosSinalizadas: Number(d.totalDuplicatasEntreArquivosSinalizadas ?? 0),
    totalDuplicatasEntreArquivosIgnoradas: Number(d.totalDuplicatasEntreArquivosIgnoradas ?? 0),
    totalSugestoesVinculo: Number(d.totalSugestoesVinculo ?? 0),
    faturaId: (d.faturaId as string | null) ?? null,
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

export interface ExcluirLancamentoResultado {
  transferenciaExcluida: boolean
  tituloReaberto: string | null
}

export const excluirLancamento = async (
  contaId: string,
  item: { id: string; data: string },
): Promise<ExcluirLancamentoResultado> => {
  const res = await apiFetch<ApiResponse<unknown>>(
    `/api/contas-bancarias/${contaId}/excluir-lancamento`,
    { method: 'POST', body: JSON.stringify(item) },
  )
  const d = res.dados as Record<string, unknown>
  return {
    transferenciaExcluida: Boolean(d.transferenciaExcluida),
    tituloReaberto: (d.tituloReaberto as string | null) ?? null,
  }
}
