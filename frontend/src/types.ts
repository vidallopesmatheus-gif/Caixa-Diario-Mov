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

export interface ItemFinanceiro {
  descricao: string
  valor: number
  categoria?: string
  tipoCusto?: 'Receita' | 'CustoFixo' | 'CustoVariavel'
}

export interface ItemFinanceiroSaida {
  descricao: string
  valor: number
  categoria: string
  subcategoria: string
  tipoCusto?: 'Receita' | 'CustoFixo' | 'CustoVariavel'
}


export interface ContaProvisionada {
  descricao: string
  valor: number
  dataVencimento?: string
  pago: boolean
  categoria?: string
  recorrenciaId?: string
  dataBaixa?: string
}

export interface Registro {
  id: string
  clienteId: string
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
  tipoCusto: 'Receita' | 'CustoFixo' | 'CustoVariavel'
  grupo?: string
}

export interface Categorias {
  entradas: CategoriaItem[]
  saidas: CategoriaItem[]
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

export interface ChatResponse {
  dados: {
    reply: string
    wasBlocked: boolean
  }
  codigoRetorno: string
  mensagem: string
}
