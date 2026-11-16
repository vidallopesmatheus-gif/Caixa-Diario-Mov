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
  /** Sem par real e com o extrato da outra conta cobrindo a data: cria a contrapartida mesmo assim. */
  criarMesmoAssim?: boolean
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
      CriarMesmoAssim: dto.criarMesmoAssim ?? false,
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

// ── Money Vision ("transferência cria contrapartida falsa"): antes de converter, a tela mostra o
// lançamento real da outra conta com que vai parear — ou avisa que o extrato dela não tem esse valor.

export interface CandidatoContrapartida {
  id: string
  data: string
  descricao: string
  /** Com sinal: entrada positiva, saída negativa. */
  valor: number
}

export interface CandidatosContrapartida {
  contaContrapartidaNome: string
  /** Sem candidato e com extrato cobrindo a data: "o extrato do C6 não tem esse recebimento". */
  extratoCobreData: boolean
  candidatos: CandidatoContrapartida[]
}

export const listarCandidatosContrapartida = async (params: {
  contaId: string; lancamentoId: string; data: string; contaContrapartidaId: string
}): Promise<CandidatosContrapartida> => {
  const query = new URLSearchParams(params)
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const res = await apiFetch<ApiResponse<any>>(`/api/transferencias/candidatos-contrapartida?${query}`)
  const d = res.dados ?? {}
  return {
    contaContrapartidaNome: d.contaContrapartidaNome ?? '',
    extratoCobreData: !!d.extratoCobreData,
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    candidatos: (d.candidatos ?? []).map((c: any) => ({ id: c.id, data: String(c.data).slice(0, 10), descricao: c.descricao ?? '', valor: c.valor ?? 0 })),
  }
}

// ── "Transferências para revisar" ─────────────────────────────────────────────────────────────

export interface ItemCorrecaoTransferencia {
  transferenciaId: string
  tipo: 'DuplicadaComReal' | 'ParDuplicado' | 'AguardandoExtrato' | 'SemLancamentoNoExtrato'
  acao: string
  contaNome: string
  data: string
  valor: number
  descricaoCriada: string
  descricaoReal?: string
}

export interface TransferenciaParaRevisar {
  transferenciaId: string
  data: string
  valor: number
  contaCriadaNome: string
  contaOutraNome: string
  descricaoOriginal: string
}

export interface RevisaoTransferencias {
  corrigiveis: ItemCorrecaoTransferencia[]
  paraDecidir: TransferenciaParaRevisar[]
}

export const listarTransferenciasParaRevisar = async (clienteId: string): Promise<RevisaoTransferencias> => {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const res = await apiFetch<ApiResponse<any>>(`/api/transferencias/${clienteId}/revisar`)
  const d = res.dados ?? {}
  return {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    corrigiveis: (d.corrigiveis ?? []).map((c: any) => ({ ...c, data: String(c.data).slice(0, 10), descricaoReal: c.descricaoReal ?? undefined })),
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    paraDecidir: (d.paraDecidir ?? []).map((p: any) => ({ ...p, data: String(p.data).slice(0, 10) })),
  }
}

export const corrigirContrapartidasTransferencia = async (clienteId: string): Promise<void> => {
  await apiFetch<ApiResponse<unknown>>(`/api/transferencias/${clienteId}/revisar/corrigir`, { method: 'POST' })
}

/** "Foi para essa conta mesmo" — sai da revisão. */
export const confirmarContrapartidaTransferencia = async (transferenciaId: string): Promise<void> => {
  await apiFetch<ApiResponse<null>>(`/api/transferencias/${transferenciaId}/confirmar-contrapartida`, { method: 'POST' })
}

/** "Não foi para essa conta" — apaga a contrapartida inventada; o lançamento real volta a pendente. */
export const descartarContrapartidaTransferencia = async (transferenciaId: string): Promise<void> => {
  await apiFetch<ApiResponse<null>>(`/api/transferencias/${transferenciaId}/descartar-contrapartida`, { method: 'POST' })
}
