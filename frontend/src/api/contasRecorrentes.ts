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
    contaBancariaId: raw.contaBancariaId ?? undefined,
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
  contaBancariaId?: string
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
      contaBancariaId: dto.contaBancariaId,
    }),
  })
  return mapContaRecorrente(res.dados)
}

/**
 * `aplicarAsPendentes` decide o que acontece com ocorrências já geradas e ainda não pagas dessa
 * recorrência: false (padrão) = só vale a partir da próxima ocorrência; true = também atualiza
 * Descrição/Valor/Categoria/ContaBancariaId das pendentes já materializadas (as já pagas nunca
 * são tocadas — a data de vencimento de cada ocorrência também nunca muda por aqui).
 */
export async function atualizarContaRecorrente(clienteId: string, id: string, dto: {
  descricao?: string
  valor?: number
  categoria?: string
  dataInicio?: string
  dataFim?: string
  periodicidade?: string
  contaBancariaId?: string
  aplicarAsPendentes?: boolean
}): Promise<ContaRecorrente> {
  const res = await apiFetch<ApiResponse<unknown>>(`/api/contas-recorrentes/${clienteId}/${id}`, {
    method: 'PUT',
    body: JSON.stringify({
      descricao: dto.descricao,
      valor: dto.valor,
      categoria: dto.categoria,
      dataInicio: dto.dataInicio,
      dataFim: dto.dataFim,
      periodicidade: dto.periodicidade,
      contaBancariaId: dto.contaBancariaId,
      aplicarAsPendentes: dto.aplicarAsPendentes ?? false,
    }),
  })
  return mapContaRecorrente(res.dados)
}

/** `removerPendentes` decide se as ocorrências pendentes (não pagas) já geradas somem junto —
 * as já pagas nunca são removidas (apagaria um fato financeiro já refletido no saldo). */
export async function desativarContaRecorrente(clienteId: string, id: string, removerPendentes = false): Promise<void> {
  await apiFetch(`/api/contas-recorrentes/${clienteId}/${id}?removerPendentes=${removerPendentes}`, { method: 'DELETE' })
}
