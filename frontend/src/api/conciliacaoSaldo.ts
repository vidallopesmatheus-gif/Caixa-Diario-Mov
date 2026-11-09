import { apiFetch } from './client'
import type {
  ApiResponse, CoberturaExtrato, DiagnosticoDiferenca, LinhaDiagnostico, PontoConciliacao, StatusConciliacao,
} from '../types'

// Datas chegam como "yyyy-MM-dd" (DateOnly) — normaliza qualquer sufixo de hora por segurança.
const dia = (v: unknown): string => String(v ?? '').slice(0, 10)

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export function mapPonto(raw: any): PontoConciliacao {
  return {
    data: dia(raw.data), saldoBanco: raw.saldoBanco ?? 0, saldoApp: raw.saldoApp ?? 0,
    diferenca: raw.diferenca ?? 0, conciliado: !!raw.conciliado, origem: raw.origem ?? '',
  }
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export function mapStatusConciliacao(raw: any): StatusConciliacao {
  return {
    extratoAte: raw.extratoAte ? dia(raw.extratoAte) : undefined,
    diasSemExtrato: raw.diasSemExtrato ?? undefined,
    saldoApp: raw.saldoApp ?? 0,
    ultimoPonto: raw.ultimoPonto ? mapPonto(raw.ultimoPonto) : undefined,
    divergenteDesde: raw.divergenteDesde ? dia(raw.divergenteDesde) : undefined,
  }
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export function mapLinhaDiagnostico(raw: any): LinhaDiagnostico {
  return {
    id: raw.id, data: dia(raw.data), descricao: raw.descricao ?? '', valor: raw.valor ?? 0,
    categoria: raw.categoria ?? undefined, efeitoSeCorrigir: raw.efeitoSeCorrigir ?? 0,
  }
}

export const obterCoberturaExtrato = async (contaId: string): Promise<CoberturaExtrato> => {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const res = await apiFetch<ApiResponse<any>>(`/api/contas-bancarias/${contaId}/cobertura`)
  const d = res.dados ?? {}
  return {
    diasComExtrato: (d.diasComExtrato ?? []).map(dia),
    pontos: (d.pontos ?? []).map(mapPonto),
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    saldosPorDia: (d.saldosPorDia ?? []).map((s: any) => ({ data: dia(s.data), saldoFinal: s.saldoFinal ?? 0 })),
    status: mapStatusConciliacao(d.status ?? {}),
  }
}

/** Diagnóstico da diferença app × banco numa data — sem saldoBanco, usa o do ponto já registrado. */
export const diagnosticarSaldo = async (contaId: string, data: string, saldoBanco?: number): Promise<DiagnosticoDiferenca> => {
  const params = new URLSearchParams({ data })
  if (saldoBanco !== undefined) params.set('saldoBanco', String(saldoBanco))
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const res = await apiFetch<ApiResponse<any>>(`/api/contas-bancarias/${contaId}/diagnostico-saldo?${params}`)
  const d = res.dados ?? {}
  return {
    data: dia(d.data), saldoBanco: d.saldoBanco ?? 0, saldoApp: d.saldoApp ?? 0, diferenca: d.diferenca ?? 0,
    analisadoDesde: dia(d.analisadoDesde),
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    lacunas: (d.lacunas ?? []).map((l: any) => ({ de: dia(l.de), ate: dia(l.ate) })),
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    duplicatas: (d.duplicatas ?? []).map((g: any) => ({ itens: (g.itens ?? []).map(mapLinhaDiagnostico), somaExcedente: g.somaExcedente ?? 0 })),
    sinalSuspeito: (d.sinalSuspeito ?? []).map(mapLinhaDiagnostico),
    transferenciasSemPar: (d.transferenciasSemPar ?? []).map(mapLinhaDiagnostico),
    manuais: (d.manuais ?? []).map(mapLinhaDiagnostico),
    combinacao: d.combinacao ? { resumo: d.combinacao.resumo ?? '', itens: (d.combinacao.itens ?? []).map(mapLinhaDiagnostico) } : undefined,
  }
}

/** "Saldo no banco em 07/11" digitado — grava o ponto de conciliação, nunca cria ajuste. */
export const registrarSaldoBanco = async (contaId: string, data: string, saldoBanco: number): Promise<PontoConciliacao> => {
  const res = await apiFetch<ApiResponse<unknown>>(`/api/contas-bancarias/${contaId}/saldo-banco`, {
    method: 'POST',
    body: JSON.stringify({ data, saldoBanco }),
  })
  return mapPonto(res.dados)
}
