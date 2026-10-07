import { apiFetch } from './client'
import type { ApiResponse, ContaProvisionada } from '../types'

const GUID_VAZIO = '00000000-0000-0000-0000-000000000000'

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function mapContaProvisionada(raw: any): ContaProvisionada {
  return {
    id: raw.id && raw.id !== GUID_VAZIO ? raw.id : undefined,
    descricao: raw.descricao,
    valor: raw.valor,
    dataVencimento: raw.dataVencimento,
    pago: raw.pago,
    categoria: raw.categoria,
    recorrenciaId: raw.recorrenciaId,
    dataBaixa: raw.dataBaixa,
    valorRealizado: raw.valorRealizado ?? undefined,
    contaBancariaId: raw.contaBancariaId,
    lancamentoVinculadoId: raw.lancamentoVinculadoId ?? undefined,
  }
}

// Fase 0.4: CRUD de conta a pagar/receber por Id, sem reenviar o RegistroDiario inteiro.
export async function criarContaProvisionada(dto: {
  clienteId: string
  contaBancariaId: string
  tipo: 'Receber' | 'Pagar'
  descricao: string
  valor: number
  dataVencimento?: string
  categoria?: string
}): Promise<ContaProvisionada> {
  const res = await apiFetch<ApiResponse<unknown>>('/api/contas', {
    method: 'POST',
    body: JSON.stringify({
      clienteId: dto.clienteId, contaBancariaId: dto.contaBancariaId, tipo: dto.tipo,
      descricao: dto.descricao, valor: dto.valor, dataVencimento: dto.dataVencimento, categoria: dto.categoria,
    }),
  })
  return mapContaProvisionada(res.dados)
}

// Cobre edição simples (qualquer campo abaixo != undefined muda) E baixa/estorno (via `pago`).
// Baixa sempre resulta num lançamento real: informe lancamentoVinculadoId para linkar a um já
// existente, ou deixe de fora pra criar um novo na contaBancariaId+dataPagamento do item.
export async function atualizarContaProvisionada(clienteId: string, id: string, dto: {
  descricao?: string
  valor?: number
  dataVencimento?: string
  categoria?: string
  contaBancariaId?: string
  pago?: boolean
  dataPagamento?: string
  valorRealizado?: number
  lancamentoVinculadoId?: string
}): Promise<ContaProvisionada> {
  const res = await apiFetch<ApiResponse<unknown>>(`/api/contas/${clienteId}/${id}`, {
    method: 'PUT',
    body: JSON.stringify({
      descricao: dto.descricao, valor: dto.valor, dataVencimento: dto.dataVencimento, categoria: dto.categoria,
      contaBancariaId: dto.contaBancariaId, pago: dto.pago, dataPagamento: dto.dataPagamento,
      valorRealizado: dto.valorRealizado, lancamentoVinculadoId: dto.lancamentoVinculadoId,
    }),
  })
  return mapContaProvisionada(res.dados)
}

// Só permite excluir título ainda pendente — um já pago precisa ser estornado antes (ver
// atualizarContaProvisionada com pago: false).
export async function excluirContaProvisionada(clienteId: string, id: string): Promise<void> {
  await apiFetch<ApiResponse<null>>(`/api/contas/${clienteId}/${id}`, { method: 'DELETE' })
}
