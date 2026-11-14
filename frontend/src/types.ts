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

export type TipoCusto =
  | 'Receita' | 'CustoFixo' | 'CustoVariavel' | 'DespesaNaoOperacional' | 'Investimento' | 'Financiamento'
  | 'RetiradaSocios'

// Rodada de ajustes Money Vision (item 8): de que lado do combobox de lançamento a categoria
// aparece — tem um padrão derivado do Bloco (ver backend CategoriaService.TipoMovimentoPadrao),
// mas é editável em Configurações > Plano de Contas.
export type TipoMovimento = 'Entrada' | 'Saida' | 'Ambos'

// Os 7 blocos são fixos na estrutura do demonstrativo — não são cadastráveis, só os Grupos dentro
// de cada um são (ver Configurações → Plano de Contas).
export type Bloco =
  | 'RECEITAS OPERACIONAIS'
  | 'DEDUÇÕES DA RECEITA'
  | 'CUSTOS OPERACIONAIS'
  | 'DESPESAS OPERACIONAIS'
  | 'ATIVIDADES DE INVESTIMENTO'
  | 'ATIVIDADES DE FINANCIAMENTO'
  | 'RETIRADAS DOS SÓCIOS'

export const BLOCOS_ORDEM: Bloco[] = [
  'RECEITAS OPERACIONAIS', 'DEDUÇÕES DA RECEITA', 'CUSTOS OPERACIONAIS',
  'DESPESAS OPERACIONAIS', 'ATIVIDADES DE INVESTIMENTO', 'ATIVIDADES DE FINANCIAMENTO',
  'RETIRADAS DOS SÓCIOS',
]

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
  // Verdadeiro quando quem escolheu a categoria foi o cliente, pelo portal de conciliação
  // (link público) — nunca setado quando o consultor categoriza pela tela normal.
  classificadoPeloCliente?: boolean
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
  classificadoPeloCliente?: boolean
  // Item 9: compra parcelada no cartão — cada parcela é sua própria saída (ver FaturaCartaoService.
  // LancarComprasParceladaAsync), ligadas por parcelamentoId; numeroParcela/totalParcelas são só exibição.
  parcelamentoId?: string
  numeroParcela?: number
  totalParcelas?: number
}


export interface ContaProvisionada {
  // Ausente só em itens muito antigos, nunca resalvos desde a Fase 0.4 — editar/excluir por Id
  // exige que esteja presente (ver api/contasProvisionadas.ts).
  id?: string
  descricao: string
  valor: number
  dataVencimento?: string
  pago: boolean
  categoria?: string
  recorrenciaId?: string
  dataBaixa?: string
  // Preenchido só quando o valor pago (juros/multa/desconto) difere do valor original do título.
  valorRealizado?: number
  contaBancariaId?: string
  // Preenchido quando a baixa foi vinculada a um lançamento (Entrada/Saída) já existente,
  // em vez de gerar um novo — evita contar o mesmo dinheiro duas vezes no saldo.
  lancamentoVinculadoId?: string
  // Rodada "Visão de Futuro" item 6: permite agrupar as parcelas de um mesmo contrato de
  // financiamento na lista de Contas a Pagar, em vez de uma a uma.
  origemFinanciamentoId?: string
}

export interface MetaVinculada {
  id: string
  ano: number
  sonho?: string
  valorSonho: number
  // Money Vision — Bloco 5: "faltam R$X; no seu ritmo de aporte você chega lá em <mês/ano>".
  previsaoAtingimentoMesAno?: string | null
}

export interface ContaBancaria {
  id: string
  clienteId: string
  nome: string
  tipo: 'Caixa' | 'ContaCorrente' | 'Investimento' | 'CartaoCredito'
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
  // Money Vision — Bloco 5: resumo do MÊS (diferente de totalAportado/rendimentoAcumulado acima,
  // que são desde o início da conta) e rentabilidade mês/ano.
  aporteMesAtual?: number
  resgateMesAtual?: number
  aporteLiquidoMesAtual?: number
  rendimentoMesAtual?: number
  rentabilidadeMesAtualPercentual?: number | null
  rentabilidadeAnoAtualPercentual?: number | null
  ultimaAtualizacaoRendimento?: string | null
  // Só vêm preenchidos quando tipo === 'CartaoCredito'. saldoDevedor é sempre >= 0 (valor
  // absoluto da dívida), mesmo que saldoAtual seja negativo.
  limite?: number | null
  diaFechamento?: number | null
  diaVencimento?: number | null
  saldoDevedor?: number
  limiteDisponivel?: number | null
  // Money Vision: "card mostra R$ 0,00, mas as faturas dizem outra coisa" (item 4) — soma do
  // saldoDevedor de cada Fatura real não paga (mesma fonte do botão "Pagar" da tela de Faturas),
  // sempre preferir isto a saldoDevedor (que é só o ledger inteiro da conta) para exibição.
  faturasEmAberto?: number
  comprasMes?: number
  pagamentosMes?: number
  // Money Vision: "Extrato até 07/11 · Banco em 07/11 R$ X ✅".
  conciliacao?: StatusConciliacao
  /** "🔒 Conferido até 07/11/2026": nada que mexa no saldo muda até esta data sem reabrir. */
  travadoAte?: string
  travaSaldoBanco?: number
  /** Conferido pelo consultor — o cliente não reabre sozinho. */
  travadoPorConsultor?: boolean
}

export interface LancamentoExtrato {
  // Ausente nas linhas sintéticas de recebimento/pagamento de Contas a Pagar/Receber.
  id?: string
  data: string
  descricao: string
  categoria?: string
  valor: number
  saldoAcumulado: number
  pendenteCategorizacao: boolean
  // Preenchido só quando categoria === 'Transferência' — id do registro de Transferencia, usado
  // pra desfazer a classificação.
  transferenciaId?: string
  // Preenchido quando a categoria veio de uma RegraCategorizacao aplicada na importação (ou
  // aplicação retroativa) — corrigir manualmente não muda isso, só marca a origem pra exibição.
  regraCategorizacaoId?: string
  // Marcador de ItemFinanceiro/ItemFinanceiroSaida (ver LancamentoFiltro no backend) — "AjusteSaldo"
  // é o único valor usado hoje no front (chip cinza/itálico "Ajuste de saldo", nunca editável).
  tipoCusto?: string
  // Money Vision: "data da parcela vs. competência" — preenchido só quando esta linha é uma
  // parcela (numero > 1) cuja data foi reancorada pro fechamento/vencimento da fatura.
  dataCompraOriginal?: string
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
  // Item 3.3: só preenchido na resposta de salvar — sugestões de vínculo que o motor de
  // conciliação encontrou pra esse dia/conta logo depois do save.
  totalSugestoesVinculo?: number
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
  contaBancariaId: string
  // Fase 1.1: quando true, o valor previsto de cada ocorrência é recalculado (média das últimas
  // 3 pagas) em vez de usar `valor` cadastrado fixo.
  valorVariavel: boolean
  // Override do dia de vencimento (1-31), independente de dataInicio.
  diaVencimento?: number
}

/** Fase 6: financiamento/empréstimo/consórcio/parcelamento. Estatísticas vêm sempre calculadas
 * ao vivo pelo backend a partir das parcelas — nunca duplicadas aqui. */
export interface Financiamento {
  id: string
  descricao: string
  tipo: 'Financiamento' | 'Emprestimo' | 'Consorcio' | 'Parcelamento'
  valorBem: number
  valorEntrada: number
  valorFinanciado: number
  numeroParcelas: number
  valorParcela?: number
  taxaJurosMensal?: number
  sistema: 'Price' | 'SAC' | 'SemJuros'
  dataPrimeiraParcela: string
  contaBancariaId: string
  contaBancariaNome: string
  bemVinculado?: string
  status: 'Ativo' | 'Quitado'
  saldoDevedor: number
  totalParcelasAtual: number
  parcelasPagas: number
  percentualParcelasPagas: number
  percentualDividaQuitada: number
  jurosPagos: number
  jurosAPagar: number
  jurosTotais: number
  proximaParcelaValor?: number
  proximaParcelaVencimento?: string
  dataPrevistaQuitacao?: string
  valorAdiantado: number
  economiaJurosAcumulada: number
}

export interface ParcelaFinanciamento {
  id: string
  // "Adiantamento" é uma linha sintética (amortização extraordinária) inserida na posição
  // cronológica certa — sem Numero, Juros/Encargos sempre 0, Pago sempre true.
  tipo: 'Parcela' | 'Adiantamento'
  numero: number | null
  vencimento: string
  valor: number
  juros: number
  amortizacao: number
  encargos: number
  saldoDevedor: number
  pago: boolean
  dataBaixa?: string
  atrasada: boolean
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
  tipoMovimento: TipoMovimento
  grupoId: string
  grupoNome: string
  bloco: Bloco
  ordem: number
  ativa: boolean
  ehEntrada: boolean
  ehSaida: boolean
}

/** Grupo — nível entre Bloco (fixo) e Categoria, criado/editado em Configurações > Plano de Contas. */
export interface Grupo {
  id: string
  nome: string
  bloco: Bloco
  ordem: number
  ativo: boolean
  quantidadeCategorias: number
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
  criadoEm: string
  sonho?: string
  modoMeta: 'simples' | 'metodo'
  valorSonho: number
  prazoAnos: number
  taxaRetorno: number
  totalInvestido: number
  margemPJ?: number
  iconeSonho?: string
  contaInvestimentoId?: string
  /** Data real escolhida pra atingir o objetivo (modo "metodo") — "YYYY-MM-DD". */
  dataAlvo?: string

  // Fase 5: "🏆 Conquistei"
  status: 'EmAndamento' | 'Conquistada' | 'Cancelada'
  dataConquista?: string
  valorPago?: number
  formaPagamento?: 'AVista' | 'Cartao' | 'Boleto' | 'Financiado'
  parcelas?: number
  dataPrimeiraParcela?: string
  contaPagamentoId?: string
  categoria?: string
  observacao?: string
  transferenciaResgateId?: string
  financiamentoId?: string
  // Item 5 (Money Vision): "Ativo" | "Quitado" do contrato vinculado — "Ativo" mostra "Quitar
  // <bem>" em vez de só "Conquistada".
  financiamentoStatus?: 'Ativo' | 'Quitado'
  // "Conquistei → Já lancei essa compra": parcelamentos/lançamentos vinculados à meta.
  parcelamentoIds?: string[]
  lancamentoIds?: string[]
  andamento?: AndamentoConquista
}

/** "Pago R$ 409,16 de R$ 4.091,60" — só em metas conquistadas com algo vinculado/lançado. */
export interface AndamentoConquista {
  valorPago: number
  valorTotal: number
  parcelamentos: ParcelamentoDaMeta[]
  /** "YYYY-MM-DD" da última parcela do parcelamento que termina por último. */
  dataTermino?: string
  /** Soma das parcelas mensais — o que sobra por mês depois de dataTermino. */
  valorMensal: number
}

export interface ParcelamentoDaMeta {
  id: string
  descricao: string
  banco?: string
  parcelasPagas: number
  totalParcelas: number
  valorParcela: number
  valorTotal: number
  dataUltimaParcela: string
}

export interface LoginResponse {
  token: string
  /** Quando o access token vence (ISO, UTC) — só em memória, nunca salvo no navegador. */
  expiraEm?: string
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

/** Fase 1.7: transação que bate com um lançamento manual (sem FitId, sem histórico de
 * importação) — a única coisa que a tela de revisão mostra linha a linha. */
export interface DuplicataManual {
  transacaoIndice: number
  descricaoBanco: string
  dataBanco: string
  valor: number
  tipo: 'Entrada' | 'Saida'
  lancamentoManualId: string
  descricaoManual: string
  dataManual: string
}

/** Fase 1.7/3.1: provável duplicata entre dois arquivos importados (ex.: CSV + OFX do mesmo
 * banco) — por padrão NÃO é importada; o usuário decide por linha (ver ResolucaoDuplicata). */
export interface DuplicataEntreArquivos {
  transacaoIndice: number
  descricaoBanco: string
  dataBanco: string
  valor: number
  tipo: 'Entrada' | 'Saida'
  descricaoJaImportada: string
  dataJaImportada: string
}

/** Transação encontrada no arquivo antes de qualquer persistência — só pré-visualização. */
/** Resumo agregado do arquivo antes de importar — sem lista linha a linha, exceto as duplicatas
 * manuais (Fase 1.7), que são as únicas que pedem uma decisão explícita do usuário. */
export interface ResumoImportacao {
  totalEncontradas: number
  totalJaImportadas: number
  totalNovas: number
  totalEntradas: number
  totalSaidas: number
  quantidadeEntradas: number
  quantidadeSaidas: number
  // Só relevante pra conta CartaoCredito — ver ImportacaoService.NormalizarSinalParaCartao.
  sinalInvertidoParaCartao: boolean
  dataInicioArquivo: string
  dataFimArquivo: string
  duplicatasManuais: DuplicataManual[]
  duplicatasEntreArquivos: DuplicataEntreArquivos[]
  // Money Vision (período sobreposto): "Arquivo: 01/10 a 07/11 · já havia no app: 01/10 a 15/10 · novos dias: 16/10 a 07/11".
  cobertura?: CoberturaImportacao
  // Importados antes, dentro do período do arquivo, que não aparecem no extrato novo (manter/excluir).
  ausentesNoArquivo?: LinhaDiagnostico[]
  // Saldo que o próprio arquivo informa (OFX LEDGERBAL ou coluna "Saldo"). Sem ele, a prévia pergunta.
  saldoBancoArquivo?: { data: string; saldo: number }
  /** Linhas novas com data no período conferido da conta — não entram na importação. */
  travadoAte?: string
  linhasEmPeriodoTravado?: LinhaDiagnostico[]
}

export interface ResultadoImportacao {
  totalImportadas: number
  totalIgnoradasPeriodoConferido?: number
  totalPendentesCategorizacao: number
  totalCategorizadasPorRegra: number
  totalConciliadasTransferencia: number
  totalAmbiguasTransferencia: number
  totalEntradas: number
  totalSaidas: number
  totalMescladasComManual: number
  totalDuplicatasEntreArquivosSinalizadas: number
  totalDuplicatasEntreArquivosIgnoradas: number
  totalSugestoesVinculo: number
  // Cartão (Bloco 2): preenchido só quando a conta é CartaoCredito — a fatura criada por esta importação.
  faturaId: string | null
  // Rodada "parcelamentos": um item por Parcelamento resolvido/criado nesta importação.
  parcelamentosDetectados: ParcelamentoDetectadoResumo[]
  // A detecção de parcelamento nunca derruba a importação — se falhar numa linha, a compra entra
  // normal (sem vínculo) e o motivo aparece aqui, em vez de nunca ter importado nada.
  avisosParcelamento: string[]
  // Saldo do app × saldo do banco depois de importar — diferente: abre o diagnóstico, nunca ajusta sozinho.
  conciliacao?: PontoConciliacao
  totalAusentesExcluidos?: number
}

export interface ParcelamentoDetectadoResumo {
  parcelamentoId: string
  comerciante: string
  numeroParcela: number
  totalParcelas: number
  parcelasRestantes: number
  valorRestante: number
  status: string
}

/** Lançamento já real (afeta saldo) que ainda não tem categoria. */
export interface PendenteCategorizacao {
  id: string
  data: string
  descricao: string
  valor: number
  tipo: 'Entrada' | 'Saida'
  // Item 3.4: histórico do cliente (≥2x) ou dicionário padrão já sugeriram uma categoria — ainda
  // precisa de confirmação (diferente de uma regra do cliente, que confirma sozinha e nunca aparece aqui).
  categoria?: string
  categoriaSugerida?: boolean
  // Item 3.5: achou um único lançamento de sentido oposto, valor/data compatíveis, em outra conta
  // ativa do mesmo cliente — provável transferência entre contas próprias.
  sugestaoTransferenciaContaId?: string
  sugestaoTransferenciaContaNome?: string
  sugestaoTransferenciaLancamentoId?: string
  sugestaoTransferenciaData?: string
}

/** Regra de categorização automática aplicada na importação de extrato. */
export interface RegraCategorizacao {
  id: string
  contaBancariaId: string
  contaBancariaNome: string
  tipo: 'Entrada' | 'Saida'
  criterioTipo: 'Documento' | 'DescricaoExata'
  criterioValor: string
  descricaoReferencia: string
  acaoTipo: 'Categoria' | 'Transferencia'
  categoria?: string
  contaContrapartidaId?: string
  contaContrapartidaNome?: string
  ativa: boolean
  // Verdadeiro = sugerida pelo cliente no portal público, aguardando aprovação do consultor.
  sugerida: boolean
  ordem: number
  quantidadeAplicada: number
  criadoEm: string
}

/** Link público (sem login), válido 24h, pro cliente classificar seus pendentes sozinho. */
export interface LinkConciliacao {
  id: string
  token: string
  criadoEm: string
  expiraEm: string
  revogadoEm?: string
  ultimoAcessoEm?: string
  totalClassificadosPeloCliente: number
  status: 'Ativo' | 'Expirado' | 'Revogado'
}

export interface ContaPendentesPortal {
  contaBancariaId: string
  contaNome: string
  itens: PendenteCategorizacao[]
}

/** Dados que o portal público recebe pelo token — só o necessário pra classificar pendentes. */
export interface PortalConciliacaoData {
  expiraEm: string
  contas: ContaPendentesPortal[]
  categoriasEntrada: CategoriaItem[]
  categoriasSaida: CategoriaItem[]
}

export interface ChatResponse {
  dados: {
    reply: string
    wasBlocked: boolean
  }
  codigoRetorno: string
  mensagem: string
}

// ── Money Vision: conciliação de saldo com o banco ─────────────────────────────────────────────
export interface PontoConciliacao {
  data: string
  saldoBanco: number
  saldoApp: number
  /** Banco − app. */
  diferenca: number
  conciliado: boolean
  origem: string
  /** "Conferir saldo" que bateu: até onde a conta ficou travada. */
  travadoAte?: string
}

export interface StatusConciliacao {
  /** Último dia coberto por extrato importado. */
  extratoAte?: string
  diasSemExtrato?: number
  saldoApp: number
  ultimoPonto?: PontoConciliacao
  /** Primeiro ponto divergente depois do último conciliado. */
  divergenteDesde?: string
}

export interface CoberturaExtrato {
  diasComExtrato: string[]
  pontos: PontoConciliacao[]
  saldosPorDia: { data: string; saldoFinal: number }[]
  status: StatusConciliacao
}

export interface CoberturaImportacao {
  arquivoDe: string
  arquivoAte: string
  jaHaviaDe?: string
  jaHaviaAte?: string
  novosDiasDe?: string
  novosDiasAte?: string
  quantidadeDiasNovos: number
}

export interface LinhaDiagnostico {
  id: string
  data: string
  descricao: string
  /** Com sinal: entrada +, saída −. */
  valor: number
  categoria?: string
  /** Quanto o saldo do app muda se esta linha for corrigida. */
  efeitoSeCorrigir: number
}

export interface DiagnosticoDiferenca {
  data: string
  /** Ausentes quando não há saldo do banco informado nesta data — as causas continuam listadas. */
  saldoBanco?: number
  saldoApp: number
  diferenca?: number
  analisadoDesde: string
  lacunas: { de: string; ate: string }[]
  duplicatas: { itens: LinhaDiagnostico[]; somaExcedente: number }[]
  sinalSuspeito: LinhaDiagnostico[]
  transferenciasSemPar: LinhaDiagnostico[]
  manuais: LinhaDiagnostico[]
  /** Ajustes de saldo no período — escondem dado faltando; efeitoSeCorrigir = desfazer. */
  ajustes: LinhaDiagnostico[]
  /** Contrapartida de transferência criada pelo app que o extrato desta conta não tem. */
  contrapartidasSemLancamento: LinhaDiagnostico[]
  /** Contrapartida criada pelo app que repete um lançamento real solto (conta 2×). */
  contrapartidasDuplicadas: LinhaDiagnostico[]
  combinacao?: { resumo: string; itens: LinhaDiagnostico[] }
  /** O que foi criado, editado ou excluído desde a última conferência que bateu. */
  mudancasDesdeConferencia?: MudancaDesdeConferencia[]
}

export interface MudancaDesdeConferencia {
  tipo: 'Criado' | 'Editado' | 'Excluido'
  data: string
  descricao: string
  valorAntes?: number
  valorDepois?: number
}

/** Uma linha do histórico de conferências e reaberturas da conta. */
export interface ConferenciaSaldo {
  tipo: 'Conferencia' | 'Reabertura'
  data?: string
  saldoBanco?: number
  travadoAteAntes?: string
  travadoAteDepois?: string
  quem: 'Consultor' | 'Cliente'
  motivo?: string
  ocorridoEm: string
}

/** Ajuste de saldo da conta na tela "Conferir saldo" — confirmar ou desfazer. */
export interface AjusteSaldo {
  id: string
  data: string
  descricao: string
  /** Com sinal: entrada positiva, saída negativa. */
  valor: number
  saldoDoDiaSemAjuste: number
  saldoDoDia: number
  confirmado: boolean
}
