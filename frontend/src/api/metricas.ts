import { apiFetch } from './client'
import type { ApiResponse } from '../types'

export interface EbitdaMetrica {
  valor: number
  percentual?: number
  semaforo: string
}

export interface PrimeCostMetrica {
  percentual?: number
  semaforo: string
}

export interface PontoDeEquilibrioMetrica {
  valor: number
  receita: number
  semaforo: string
}

export interface ValuationMetrica {
  valor: number
  semaforo: string
}

export interface RunwayMetrica {
  meses: number
  semaforo: string
}

export interface LiquidezMetrica {
  indice?: number
  altaLiquidez: boolean
  semaforo: string
}

export interface TicketMedioMetrica {
  valor: number
  quantidadeRecebimentos: number
}

export interface MetricasPeriodo {
  ebitda?: EbitdaMetrica
  primeCost?: PrimeCostMetrica
  pontoDeEquilibrio?: PontoDeEquilibrioMetrica
  saldoProjetado: number
  burnRate?: number
  valuation?: ValuationMetrica
  runway?: RunwayMetrica
  liquidez?: LiquidezMetrica
  ticketMedio?: TicketMedioMetrica
}

export interface EvolucaoMensal {
  mes: string
  receita: number
  custos: number
  lucro: number
  saldo: number
}

export interface FluxoDia {
  data: string
  saldoProjetado: number
}

export interface FluxoProjetado {
  saldoAtual: number
  dias: FluxoDia[]
}

export async function obterMetricas(clienteId: string, de: string, ate: string, multiplo = 3): Promise<MetricasPeriodo> {
  const res = await apiFetch<ApiResponse<MetricasPeriodo>>(`/api/metricas/${clienteId}?de=${de}&ate=${ate}&multiplo=${multiplo}`)
  return res.dados
}

export async function obterEvolucao(clienteId: string, meses = 12): Promise<EvolucaoMensal[]> {
  const res = await apiFetch<ApiResponse<EvolucaoMensal[]>>(`/api/metricas/${clienteId}/evolucao?meses=${meses}`)
  return res.dados
}

export async function obterFluxoProjetado(clienteId: string, dias = 90): Promise<FluxoProjetado> {
  const res = await apiFetch<ApiResponse<FluxoProjetado>>(`/api/metricas/${clienteId}/fluxo-projetado?dias=${dias}`)
  return res.dados
}

export interface DreCategoria {
  nome: string
  total: number
  percentual: number | null
}

export interface DreLinha {
  grupo: string
  total: number
  categorias: DreCategoria[]
}

export interface DreLinhaVertical {
  total: number
  percentual: number | null
  categorias: DreCategoria[]
}

export interface Dre {
  receitaBruta: number
  gruposDespesa: DreLinha[]
  totalDespesas: number
  resultado: number
  margem: number | null

  // Análise vertical (base: Receita Bruta = 100%)
  receitaBrutaPercentual: number | null
  deducoes: DreLinhaVertical
  receitaLiquida: number
  receitaLiquidaPercentual: number | null
  custosVariaveis: DreLinhaVertical
  margemContribuicao: number
  margemContribuicaoPercentual: number | null
  despesasFixas: DreLinhaVertical
  resultadoOperacional: number
  resultadoOperacionalPercentual: number | null
  receitaFinanceira: DreLinhaVertical
  despesasNaoOperacionais: DreLinhaVertical
  naoClassificado: DreLinhaVertical
  resultadoLiquido: number
  resultadoLiquidoPercentual: number | null
}

export async function obterDre(
  clienteId: string,
  de: string,
  ate: string,
  contaBancariaId?: string,
): Promise<Dre> {
  const params = new URLSearchParams({ de, ate })
  if (contaBancariaId) params.set('contaBancariaId', contaBancariaId)
  const res = await apiFetch<ApiResponse<Dre>>(`/api/metricas/${clienteId}/dre?${params}`)
  return res.dados
}

export interface CategoriaIndicador {
  nome: string
  grupo: string
  total: number
  percentualReceita: number | null
  mediaMesesAnteriores: number | null
  variacaoPercentual: number | null
}

export interface PontoEquilibrio {
  disponivel: boolean
  motivoIndisponivel: string | null
  valorMensal: number | null
  valorPorDiaUtil: number | null
  diasUteisNoMes: number | null
  receitaAtual: number
  distancia: number | null
  distanciaPercentual: number | null
}

export interface FolegoCaixa {
  disponivel: boolean
  motivoIndisponivel: string | null
  saldoDisponivel: number
  custoFixoMedioMensal: number | null
  meses: number | null
  faixa: 'critico' | 'atencao' | 'confortavel' | 'indisponivel'
}

export interface CustoFixoMensal {
  mes: string
  receita: number
  custoFixo: number
  percentual: number | null
}

export interface PrazoRecebimento {
  disponivel: boolean
  motivoIndisponivel: string | null
  mediaDias: number | null
  quantidadeAmostras: number
}

export interface IndicadoresDecisao {
  dre: Dre
  custoFixo: number
  custoVariavel: number
  custoNaoClassificado: number
  rankingCategorias: CategoriaIndicador[]
  evolucao: EvolucaoMensal[]
  mesesComAtividade: number
  variacaoReceitaMesAnterior: number | null
  variacaoReceitaAnoAnterior: number | null
  pontoEquilibrio: PontoEquilibrio
  folegoCaixa: FolegoCaixa
  custoFixoMensal: CustoFixoMensal[]
  prazoRecebimento: PrazoRecebimento
}

export async function obterIndicadores(clienteId: string, mesesEvolucao = 13): Promise<IndicadoresDecisao> {
  const res = await apiFetch<ApiResponse<IndicadoresDecisao>>(
    `/api/metricas/${clienteId}/indicadores?mesesEvolucao=${mesesEvolucao}`,
  )
  return res.dados
}
