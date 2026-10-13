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

export const listarTransferencias = async (clienteId: string): Promise<Transferencia[]> => {
  const res = await apiFetch<ApiResponse<unknown[]>>(`/api/transferencias/${clienteId}`)
  return (res.dados ?? []).map(mapTransferencia)
}

/**
 * Desfaz a classificação como Transferência — os dois lançamentos voltam a ficar sem categoria
 * (pendentes de categorização). Nunca apaga o lançamento em si: o dinheiro realmente se moveu.
 */
export const desfazerClassificacaoTransferencia = async (id: string): Promise<void> => {
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

// ── Bloco 4, item 4 (Money Vision): "Transferências sem par" — sugestões da importação
// (dicionário de palavras-chave) ainda não confirmadas. ──────────────────────────────────────

export interface SugestaoTransferenciaSemPar {
  contaBancariaId: string
  contaBancariaNome: string
  lancamentoId: string
  data: string
  descricao: string
  valor: number
  tipo: 'Entrada' | 'Saida'
  contaDestinoSugeridaId?: string
  contaDestinoSugeridaNome?: string
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function mapSugestaoSemPar(raw: any): SugestaoTransferenciaSemPar {
  return {
    contaBancariaId: raw.contaBancariaId,
    contaBancariaNome: raw.contaBancariaNome ?? '',
    lancamentoId: raw.lancamentoId,
    data: raw.data,
    descricao: raw.descricao ?? '',
    valor: raw.valor ?? 0,
    tipo: raw.tipo,
    contaDestinoSugeridaId: raw.contaDestinoSugeridaId ?? undefined,
    contaDestinoSugeridaNome: raw.contaDestinoSugeridaNome ?? undefined,
  }
}

export const listarSugestoesTransferenciaSemPar = async (clienteId: string): Promise<SugestaoTransferenciaSemPar[]> => {
  const res = await apiFetch<ApiResponse<unknown[]>>(`/api/transferencias/${clienteId}/sugestoes-sem-par`)
  return (res.dados ?? []).map(mapSugestaoSemPar)
}

/** Remove a sugestão "Transferência" sem confirmar nada — o lançamento volta a ser um pendente comum. */
export const descartarSugestaoTransferencia = async (dto: { contaId: string; lancamentoId: string; data: string }): Promise<void> => {
  await apiFetch<ApiResponse<null>>('/api/transferencias/descartar-sugestao', {
    method: 'POST',
    body: JSON.stringify({ ContaId: dto.contaId, LancamentoId: dto.lancamentoId, Data: dto.data }),
  })
}
