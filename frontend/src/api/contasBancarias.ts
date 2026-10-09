import { apiFetch } from './client'
import type { ApiResponse, ContaBancaria, LancamentoExtrato, PendenciasConta } from '../types'

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function mapConta(raw: any): ContaBancaria {
  return {
    id: raw.id,
    clienteId: raw.clienteId,
    nome: raw.nome,
    tipo: raw.tipo,
    saldoInicial: raw.saldoInicial ?? 0,
    saldoAtual: raw.saldoAtual ?? 0,
    entradasMes: raw.entradasMes ?? 0,
    saidasMes: raw.saidasMes ?? 0,
    pendentesCategorizacao: raw.pendentesCategorizacao ?? 0,
    ativa: raw.ativa ?? true,
    dataCriacao: raw.dataCriacao ?? '',
    totalAportado: raw.totalAportado ?? undefined,
    rendimentoAcumulado: raw.rendimentoAcumulado ?? undefined,
    rentabilidadePercentual: raw.rentabilidadePercentual ?? undefined,
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    metasVinculadas: raw.metasVinculadas?.map((m: any) => ({
      id: m.id, ano: m.ano, sonho: m.sonho ?? undefined, valorSonho: m.valorSonho ?? 0,
    })),
    progressoCombinadoPercentual: raw.progressoCombinadoPercentual ?? undefined,
    limite: raw.limite ?? undefined,
    diaFechamento: raw.diaFechamento ?? undefined,
    diaVencimento: raw.diaVencimento ?? undefined,
    saldoDevedor: raw.saldoDevedor ?? undefined,
    limiteDisponivel: raw.limiteDisponivel ?? undefined,
  }
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function mapLancamento(raw: any): LancamentoExtrato {
  return {
    id: raw.id ?? undefined,
    data: raw.data,
    descricao: raw.descricao ?? '',
    categoria: raw.categoria ?? undefined,
    valor: raw.valor ?? 0,
    saldoAcumulado: raw.saldoAcumulado ?? 0,
    pendenteCategorizacao: raw.pendenteCategorizacao ?? false,
    transferenciaId: raw.transferenciaId ?? undefined,
  }
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function mapContaProvisionada(raw: any) {
  return {
    descricao: raw.descricao ?? '',
    valor: raw.valor ?? 0,
    dataVencimento: raw.dataVencimento ?? undefined,
    pago: raw.pago ?? false,
    categoria: raw.categoria ?? undefined,
    recorrenciaId: raw.recorrenciaId ?? undefined,
    dataBaixa: raw.dataBaixa ?? undefined,
    contaBancariaId: raw.contaBancariaId ?? undefined,
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
  limite?: number
  diaFechamento?: number
  diaVencimento?: number
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
  limite?: number
  diaFechamento?: number
  diaVencimento?: number
}): Promise<ContaBancaria> => {
  const res = await apiFetch<ApiResponse<unknown>>(`/api/contas-bancarias/${id}`, {
    method: 'PUT',
    body: JSON.stringify(dto),
  })
  return mapConta(res.dados)
}

export interface ExclusaoContaBancariaResult {
  excluida: boolean
  diasComLancamento: number
  contasProvisionadas: number
  transferencias: number
  transacoesImportadas: number
  metasVinculadas: number
  totalVinculos: number
}

export const excluirOuInativarContaBancaria = async (id: string): Promise<ExclusaoContaBancariaResult> => {
  const res = await apiFetch<ApiResponse<ExclusaoContaBancariaResult>>(`/api/contas-bancarias/${id}`, { method: 'DELETE' })
  return res.dados
}

export const obterExtratoConta = async (
  contaId: string,
  de?: string,
  ate?: string,
): Promise<LancamentoExtrato[]> => {
  const params = new URLSearchParams()
  if (de) params.set('de', de)
  if (ate) params.set('ate', ate)
  const qs = params.toString()
  const res = await apiFetch<ApiResponse<unknown[]>>(
    `/api/contas-bancarias/${contaId}/extrato${qs ? `?${qs}` : ''}`,
  )
  return (res.dados ?? []).map(mapLancamento)
}

export interface DuplicataProvavel {
  tipo: 'Entrada' | 'Saida'
  score: number
  lancamentoAId: string
  descricaoA: string
  dataA: string
  valorA: number
  lancamentoBId: string
  descricaoB: string
  dataB: string
  valorB: number
}

// Fase 0.5: "Revisar duplicatas" — só sugere pares (mesma conta/sentido/valor, data ±2 dias,
// descrição parecida); excluir um dos dois usa excluirLancamento (api/importacao.ts) de sempre.
export const listarDuplicatas = async (contaId: string): Promise<DuplicataProvavel[]> => {
  const res = await apiFetch<ApiResponse<unknown[]>>(`/api/contas-bancarias/${contaId}/duplicatas`)
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  return (res.dados ?? []).map((raw: any) => ({
    tipo: raw.tipo, score: raw.score ?? 0,
    lancamentoAId: raw.lancamentoAId, descricaoA: raw.descricaoA ?? '', dataA: raw.dataA ?? '', valorA: raw.valorA ?? 0,
    lancamentoBId: raw.lancamentoBId, descricaoB: raw.descricaoB ?? '', dataB: raw.dataB ?? '', valorB: raw.valorB ?? 0,
  }))
}

// Item 3.2: "Manter os dois" — grava a decisão pra esse par não ser sugerido de novo.
export const manterDuplicata = async (contaId: string, lancamentoAId: string, lancamentoBId: string): Promise<void> => {
  await apiFetch<ApiResponse<null>>(
    `/api/contas-bancarias/${contaId}/duplicatas/manter`,
    { method: 'POST', body: JSON.stringify({ lancamentoAId, lancamentoBId }) },
  )
}

export const obterPendenciasConta = async (contaId: string): Promise<PendenciasConta> => {
  const res = await apiFetch<ApiResponse<{ recebiveis: unknown[]; pagamentos: unknown[] }>>(
    `/api/contas-bancarias/${contaId}/pendencias`,
  )
  return {
    recebiveis: (res.dados?.recebiveis ?? []).map(mapContaProvisionada),
    pagamentos: (res.dados?.pagamentos ?? []).map(mapContaProvisionada),
  }
}

export const registrarRendimento = async (
  contaId: string,
  dto: { data: string; valor: number; descricao?: string },
): Promise<ContaBancaria> => {
  const res = await apiFetch<ApiResponse<unknown>>(`/api/contas-bancarias/${contaId}/rendimento`, {
    method: 'POST',
    body: JSON.stringify(dto),
  })
  return mapConta(res.dados)
}

export const vincularMeta = async (contaId: string, metaId: string): Promise<ContaBancaria> => {
  const res = await apiFetch<ApiResponse<unknown>>(`/api/contas-bancarias/${contaId}/vincular-meta/${metaId}`, {
    method: 'POST',
  })
  return mapConta(res.dados)
}

export const desvincularMeta = async (contaId: string, metaId: string): Promise<ContaBancaria> => {
  const res = await apiFetch<ApiResponse<unknown>>(`/api/contas-bancarias/${contaId}/desvincular-meta/${metaId}`, {
    method: 'POST',
  })
  return mapConta(res.dados)
}
