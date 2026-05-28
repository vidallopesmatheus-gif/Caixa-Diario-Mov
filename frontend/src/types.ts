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
}

export interface ContaProvisionada {
  descricao: string
  valor: number
  dataVencimento?: string
  pago: boolean
}

export interface Registro {
  id: string
  clienteId: string
  data: string
  saldoInicio: number
  entradas: ItemFinanceiro[]
  saidas: ItemFinanceiro[]
  contasAReceber: ContaProvisionada[]
  contasAPagar: ContaProvisionada[]
  saldoConfirmado: number
  saldoCalculado: number
  criadoEm: string
}

export interface MetaAnual {
  id: string
  clienteId: string
  ano: number
  metaReceita: number
  metaLucro: number
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
