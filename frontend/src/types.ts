export interface Usuario {
  id: string
  nomeUsuario: string
  nomeCompleto: string
  nomeEstabelecimento: string
  perfil: 'admin' | 'cliente'
  ativo: boolean
  criadoEm: string
  criadoPor?: string
}

export type TipoCusto = 'Receita' | 'CustoFixo' | 'CustoVariavel' | 'DespesaNaoOperacional'

export interface ItemFinanceiro {
  id?: string
  descricao: string
  valor: number
  categoria?: string
  tipoCusto?: TipoCusto
  // Preenchido só quando tipoCusto === 'Transferencia'; opaco pro usuário, só precisa sobreviver
  // ao carregar/salvar de novo o dia (senão o estorno da transferência perde o vínculo).
  transferenciaId?: string
  // Opacos pro usuário — só precisam sobreviver ao carregar/salvar de novo o dia.
  fitId?: string
  pendenteCategorizacao?: boolean
}

export interface ItemFinanceiroSaida {
  id?: string
  descricao: string
  valor: number
  categoria: string
  subcategoria: string
  tipoCusto?: TipoCusto
  transferenciaId?: string
  fitId?: string
  pendenteCategorizacao?: boolean
}


export interface ContaProvisionada {
  descricao: string
  valor: number
  dataVencimento?: string
  pago: boolean
  categoria?: string
  recorrenciaId?: string
  dataBaixa?: string
  contaBancariaId?: string
  // Preenchido quando a baixa foi vinculada a um lançamento (Entrada/Saída) já existente,
  // em vez de gerar um novo — evita contar o mesmo dinheiro duas vezes no saldo.
  lancamentoVinculadoId?: string
}

export interface MetaVinculada {
  id: string
  ano: number
  sonho?: string
  valorSonho: number
}

export interface ContaBancaria {
  id: string
  clienteId: string
  nome: string
  tipo: 'Caixa' | 'ContaCorrente' | 'Investimento'
  saldoInicial: number
  saldoAtual: number
  entradasMes: number
  saidasMes: number
  pendentesCategorizacao: number
  ativa: boolean
  dataCriacao: string
  // Só vêm preenchidos quando tipo === 'Investimento'
  totalAportado?: number
  rendimentoAcumulado?: number
  rentabilidadePercentual?: number | null
  metasVinculadas?: MetaVinculada[]
  progressoCombinadoPercentual?: number | null
}

export interface LancamentoExtrato {
  data: string
  descricao: string
  categoria?: string
  valor: number
  saldoAcumulado: number
  pendenteCategorizacao: boolean
}

export interface PendenciasConta {
  recebiveis: ContaProvisionada[]
  pagamentos: ContaProvisionada[]
}

export interface Registro {
  id: string
  clienteId: string
  contaBancariaId?: string
  data: string
  saldoInicio: number
  entradas: ItemFinanceiro[]
  saidas: ItemFinanceiroSaida[]
  contasAReceber: ContaProvisionada[]
  contasAPagar: ContaProvisionada[]
  saldoConfirmado: number
  saldoCalculado: number
  criadoEm: string
}

export interface ContaRecorrente {
  id: string
  clienteId: string
  descricao: string
  valor: number
  categoria?: string
  tipo: 'Receber' | 'Pagar'
  dataInicio: string
  dataFim?: string
  periodicidade: string
  quantidadeParcelas?: number
  ativo: boolean
  criadoEm: string
}

export interface CategoriaItem {
  nome: string
  tipoCusto: TipoCusto
  grupo?: string
}

export interface Categorias {
  entradas: CategoriaItem[]
  saidas: CategoriaItem[]
}

/** Categoria completa (Configurações > Plano de Contas), inclui inativas. */
export interface CategoriaAdmin {
  id: string
  nome: string
  tipo: TipoCusto
  grupo?: string
  ordem: number
  ativa: boolean
}

export interface MetaAnual {
  id: string
  clienteId: string
  ano: number
  metaReceita: number
  metaLucro: number
  mesInicio: number
  periodoMeses: number
  salvoEm: string
  sonho?: string
  modoMeta: 'simples' | 'metodo'
  valorSonho: number
  prazoAnos: number
  taxaRetorno: number
  totalInvestido: number
  margemPJ?: number
  iconeSonho?: string
  contaInvestimentoId?: string
}

export interface LoginResponse {
  token: string
  perfil: 'admin' | 'cliente'
  nomeUsuario: string
  nomeCompleto: string
  nomeEstabelecimento: string
  usuarioId: string
}

export interface ApiResponse<T> {
  dados: T
  codigoRetorno: string
  mensagem: string
}

export interface ChatMessage {
  role: 'user' | 'assistant'
  content: string
}

/** Transação encontrada no arquivo antes de qualquer persistência — só pré-visualização. */
export interface PreviewTransacao {
  indice: number
  data: string
  valor: number
  descricao: string
  tipo: 'Entrada' | 'Saida'
  fitId?: string
  jaImportada: boolean
}

export interface ResultadoImportacao {
  totalImportadas: number
  totalPendentesCategorizacao: number
  totalEntradas: number
  totalSaidas: number
}

/** Lançamento já real (afeta saldo) que ainda não tem categoria. */
export interface PendenteCategorizacao {
  id: string
  data: string
  descricao: string
  valor: number
  tipo: 'Entrada' | 'Saida'
}

export interface ChatResponse {
  dados: {
    reply: string
    wasBlocked: boolean
  }
  codigoRetorno: string
  mensagem: string
}
