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
  }
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function mapLancamento(raw: any): LancamentoExtrato {
  return {
    data: raw.data,
    descricao: raw.descricao ?? '',
    categoria: raw.categoria ?? undefined,
    valor: raw.valor ?? 0,
    saldoAcumulado: raw.saldoAcumulado ?? 0,
    pendenteCategorizacao: raw.pendenteCategorizacao ?? false,
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
}): Promise<ContaBancaria> => {
  const res = await apiFetch<ApiResponse<unknown>>(`/api/contas-bancarias/${id}`, {
    method: 'PUT',
    body: JSON.stringify(dto),
  })
  return mapConta(res.dados)
}

export const inativarContaBancaria = async (id: string): Promise<void> => {
  await apiFetch<ApiResponse<null>>(`/api/contas-bancarias/${id}`, { method: 'DELETE' })
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
