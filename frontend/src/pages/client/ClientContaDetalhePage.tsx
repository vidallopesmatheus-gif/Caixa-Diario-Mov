import { useState, useEffect, useCallback, useMemo, useRef } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'
import {
  listarContasBancarias,
  obterExtratoConta,
  obterPendenciasConta,
  registrarRendimento,
  ajustarSaldo,
  vincularMeta,
  desvincularMeta,
  listarDuplicatas,
  manterDuplicata,
  obterHistoricoSaldoMensal,
} from '../../api/contasBancarias'
import { previewExtrato, importarExtrato, categorizarPendentes, excluirLancamento, excluirLote } from '../../api/importacao'
import type { DuplicataProvavel, SaldoMensal } from '../../api/contasBancarias'
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts'
import type { ResolucaoDuplicata } from '../../api/importacao'
import { converterLancamentoEmTransferencia, desfazerClassificacaoTransferencia, listarTransferencias } from '../../api/transferencias'
import type { Transferencia } from '../../api/transferencias'
import { listarSugestoesVinculo } from '../../api/conciliacao'
import { listarBancosCartao } from '../../api/faturasCartao'
import { atualizarContaProvisionada } from '../../api/contasProvisionadas'
import { listarRegras, atualizarRegra, criarRegra, contarCorrespondencias, aplicarRegraRetroativamente } from '../../api/regras'
import { buscarCandidatoContrapartida } from '../../utils/candidatoTransferencia'
import { listarMetas, salvarMeta } from '../../api/metas'
import { listarCategorias } from '../../api/categorias'
import { fmtBRL, fmtPct, fmtDate, todayISO, addDays } from '../../utils/format'
import { limparDescricao } from '../../utils/descricaoExibicao'
import { mesAtualISO, deslocarMes, labelMes, calcularDiasAtraso, estaNoMes } from '../../utils/contasPagarReceber'
import Modal from '../../components/shared/Modal'
import CategoriaCombobox from '../../components/shared/CategoriaCombobox'
import type { ContaBancaria, LancamentoExtrato, ContaProvisionada, MetaAnual, ResumoImportacao, ResultadoImportacao, Categorias, CategoriaAdmin } from '../../types'
import './ClientContas.css'
import './ClientContaDetalhe.css'
import './ClientContasBancarias.css'

type Aba = 'extrato' | 'recebiveis' | 'pagamentos'

const TIPO_LABEL: Record<string, string> = {
  Caixa: '💵 Caixa',
  ContaCorrente: '🏦 Conta Corrente',
  Investimento: '📈 Investimento',
  CartaoCredito: '💳 Cartão de crédito',
}

export default function ClientContaDetalhePage() {
  const { contaId } = useParams<{ contaId: string }>()
  const { user } = useAuth()
  const navigate = useNavigate()
  const fileInputRef = useRef<HTMLInputElement | null>(null)
  const clienteId = user?.usuarioId ?? ''

  const [conta, setConta] = useState<ContaBancaria | null>(null)
  const [loadingConta, setLoadingConta] = useState(true)
  const [aba, setAba] = useState<Aba>('extrato')
  const [msg, setMsg] = useState('')

  const [de, setDe] = useState(addDays(todayISO(), -30))
  const [ate, setAte] = useState(todayISO())
  const [lancamentos, setLancamentos] = useState<LancamentoExtrato[]>([])
  const [loadingExtrato, setLoadingExtrato] = useState(true)

  const [recebiveis, setRecebiveis] = useState<ContaProvisionada[]>([])
  const [pagamentos, setPagamentos] = useState<ContaProvisionada[]>([])
  const [loadingPendencias, setLoadingPendencias] = useState(true)

  // Item 7 (Parte 3, Money Vision): mesmo padrão de seletor de mês das Abas Recebíveis/Pagamentos
  // da tela de Contas a pagar/receber — sem vencimento sempre aparece (nenhum mês pra filtrar).
  const [mesPendenciasSelecionado, setMesPendenciasSelecionado] = useState(mesAtualISO())
  const [modoPendencias, setModoPendencias] = useState<'mes' | 'atrasadas' | 'tudo'>('mes')
  function filtrarPendenciasPorModo(lista: ContaProvisionada[]) {
    const pendenciaVencida = (p: ContaProvisionada) => calcularDiasAtraso(p.dataVencimento, p.pago) > 0
    if (modoPendencias === 'tudo') return lista.filter(p => !pendenciaVencida(p))
    if (modoPendencias === 'atrasadas') return lista.filter(pendenciaVencida)
    return lista.filter(p => !pendenciaVencida(p) && (!p.dataVencimento || estaNoMes(p.dataVencimento, mesPendenciasSelecionado)))
  }
  const recebiveisVisiveis = filtrarPendenciasPorModo(recebiveis)
  const pagamentosVisiveis = filtrarPendenciasPorModo(pagamentos)
  const atrasadosPendencias = [...recebiveis, ...pagamentos].filter(p => calcularDiasAtraso(p.dataVencimento, p.pago) > 0)

  // ── Importação de extrato: escolhe arquivo + intervalo, vê resumo, confirma tudo de uma vez ──
  // (Sem seleção linha a linha — a deduplicação por FITID/heurística roda automaticamente.)
  const [arquivoImportar, setArquivoImportar] = useState<File | null>(null)
  const [resumoImportacao, setResumoImportacao] = useState<ResumoImportacao | null>(null)
  const [previewCarregando, setPreviewCarregando] = useState(false)
  const [modalImportar, setModalImportar] = useState(false)
  const [dataInicioImport, setDataInicioImport] = useState('')
  const [dataFimImport, setDataFimImport] = useState('')
  const [importando, setImportando] = useState(false)
  const [resultadoImportacao, setResultadoImportacao] = useState<ResultadoImportacao | null>(null)
  // Cartão: undefined = detecção automática (padrão); true/false = usuário forçou via "Inverter
  // sinais" — escape hatch pra formato de banco que a heurística de maioria não acerta.
  const [inverterSinaisManualmente, setInverterSinaisManualmente] = useState<boolean | undefined>(undefined)
  // Fase 1.7: decisão por linha pra duplicata manual (índice ausente = Mesclar, padrão).
  // Fase 3.1: mesmo mapa pra duplicata entre arquivos (índice ausente = Ignorar, padrão).
  const [resolucoesDuplicatas, setResolucoesDuplicatas] = useState<Record<number, 'Mesclar' | 'ImportarComoNovo' | 'Ignorar' | 'Importar'>>({})
  const [confirmandoTodasSugestoes, setConfirmandoTodasSugestoes] = useState(false)
  const [sugestoesConfirmadas, setSugestoesConfirmadas] = useState(false)

  // Cartão (Bloco 2, Money Vision): cada importação numa conta CartaoCredito é UMA fatura — pede
  // banco/vencimento (obrigatórios) e fechamento/competência (opcionais, competência é sugerida
  // pelo vencimento mas editável).
  const [bancoFatura, setBancoFatura] = useState('')
  const [bancosDisponiveis, setBancosDisponiveis] = useState<string[]>([])
  const [dataVencimentoFatura, setDataVencimentoFatura] = useState('')
  const [dataFechamentoFatura, setDataFechamentoFatura] = useState('')
  const [competenciaFatura, setCompetenciaFatura] = useState('')
  const [competenciaEditadaManualmente, setCompetenciaEditadaManualmente] = useState(false)

  // ── Fase 0.5: "Revisar duplicatas" — mesma conta/sentido/valor importados duas vezes ─────────
  const [modalDuplicatas, setModalDuplicatas] = useState(false)
  const [duplicatas, setDuplicatas] = useState<DuplicataProvavel[]>([])
  const [carregandoDuplicatas, setCarregandoDuplicatas] = useState(false)
  const [excluindoDuplicataId, setExcluindoDuplicataId] = useState<string | null>(null)

  // ── Investimento: rendimento e vínculo com meta ──────────────────────────
  const [modalRendimento, setModalRendimento] = useState(false)
  // Bloco 5 (Money Vision): "Atualizar saldo" — digita o saldo que aparece no banco, o app
  // calcula a diferença e lança como Rendimento; "modoSaldo=false" preserva o fluxo antigo
  // (digitar o valor do rendimento direto), pra quem já usava assim.
  const [modoSaldo, setModoSaldo] = useState(true)
  const [saldoBancoDisplay, setSaldoBancoDisplay] = useState('')
  const [saldoBanco, setSaldoBanco] = useState(0)
  const [rendValorDisplay, setRendValorDisplay] = useState('')
  const [rendValor, setRendValor] = useState(0)
  const [rendPositivo, setRendPositivo] = useState(true)
  const [rendData, setRendData] = useState(todayISO())
  const [rendDescricao, setRendDescricao] = useState('')
  const [salvandoRendimento, setSalvandoRendimento] = useState(false)
  const [historicoSaldo, setHistoricoSaldo] = useState<SaldoMensal[]>([])

  // ── "Conferir saldo" (Money Vision, item 4): acerta o saldo com o banco através de UM
  // lançamento visível (categoria "Ajuste de saldo"), nunca uma troca silenciosa do saldo inicial
  // do dia — disponível pra qualquer conta (Investimento já tem seu próprio fluxo acima). ──
  const [modalConferirSaldo, setModalConferirSaldo] = useState(false)
  const [conferirData, setConferirData] = useState(todayISO())
  const [conferirSaldoDisplay, setConferirSaldoDisplay] = useState('')
  const [conferirSaldoReal, setConferirSaldoReal] = useState(0)
  const [salvandoConferirSaldo, setSalvandoConferirSaldo] = useState(false)

  // ── Reclassificar lançamento como Transferência (fato permutativo) ──────────
  const [contasBancarias, setContasBancarias] = useState<ContaBancaria[]>([])
  const [lancamentoParaTransferencia, setLancamentoParaTransferencia] = useState<LancamentoExtrato | null>(null)
  const [contaContrapartidaId, setContaContrapartidaId] = useState('')
  const [candidatoContrapartida, setCandidatoContrapartida] = useState<LancamentoExtrato | null>(null)
  const [buscandoCandidato, setBuscandoCandidato] = useState(false)
  const [convertendo, setConvertendo] = useState(false)

  // ── Editar categoria de lançamento já classificado (reaproveita o mesmo endpoint da
  // categorização de pendentes — ele já localiza o item por Id e não depende de estar pendente) ──
  const [categorias, setCategorias] = useState<Categorias>({ entradas: [], saidas: [] })
  // Bloco 4 (Money Vision): o chip de transferência mostra o nome da OUTRA conta ("⇄ Investimento
  // Nubank"), não o literal "Transferência" — precisa das Transferencias do cliente pra achar qual é a outra ponta.
  const [transferencias, setTransferencias] = useState<Transferencia[]>([])
  const [lancamentoParaEditar, setLancamentoParaEditar] = useState<LancamentoExtrato | null>(null)
  const [categoriaEdicao, setCategoriaEdicao] = useState('')
  const [salvandoEdicao, setSalvandoEdicao] = useState(false)
  // Atalho rápido: clicar no chip da categoria abre o combobox ali mesmo, sem o painel inteiro.
  const [chipAbertoKey, setChipAbertoKey] = useState<string | null>(null)
  const [salvandoChip, setSalvandoChip] = useState(false)
  // Layout do extrato (Money Vision — Bloco 1): confirmação de exclusão deixou de ser um
  // confirm() nativo e virou modal com botão vermelho; saldo por linha fica atrás de um toggle
  // (o padrão agora é mostrar só o saldo do dia, no cabeçalho do grupo).
  const [excluirConfirmacao, setExcluirConfirmacao] = useState<LancamentoExtrato | null>(null)
  const [excluindoLancamento, setExcluindoLancamento] = useState(false)
  const [mostrarSaldoPorLinha, setMostrarSaldoPorLinha] = useState(false)
  // Mobile (<768px): cada linha é um card; tocar nele abre uma bottom sheet com as ações.
  const [acaoMobileLancamento, setAcaoMobileLancamento] = useState<LancamentoExtrato | null>(null)

  // ── Bloco 2: barra de filtros (item 6) ───────────────────────────────────
  const [filtroTexto, setFiltroTexto] = useState('')
  const [filtroTipo, setFiltroTipo] = useState<'todos' | 'entrada' | 'saida'>('todos')
  const [filtroCategoria, setFiltroCategoria] = useState('')
  const [filtroSoSemCategoria, setFiltroSoSemCategoria] = useState(false)
  const [filtroSoTransferenciaInvestimento, setFiltroSoTransferenciaInvestimento] = useState(false)

  // ── Bloco 2: seleção em lote (item 7) ─────────────────────────────────────
  const [selecionados, setSelecionados] = useState<Set<string>>(new Set())
  const [loteCategoriaAberta, setLoteCategoriaAberta] = useState(false)
  const [loteCategoriaValor, setLoteCategoriaValor] = useState('')
  const [loteTransferenciaAberta, setLoteTransferenciaAberta] = useState(false)
  const [loteContaContrapartidaId, setLoteContaContrapartidaId] = useState('')
  const [loteExcluirAberta, setLoteExcluirAberta] = useState(false)
  const [processandoLote, setProcessandoLote] = useState(false)

  // ── Bloco 2: "aplicar a todos e criar regra?" (item 8) ────────────────────
  const [ofertaRegra, setOfertaRegra] = useState<{ lancamento: LancamentoExtrato; categoria: string; contagem: number } | null>(null)
  const [criandoOfertaRegra, setCriandoOfertaRegra] = useState(false)

  const [modalVincular, setModalVincular] = useState(false)
  const [metasDisponiveis, setMetasDisponiveis] = useState<MetaAnual[]>([])
  const [metaSelecionadaId, setMetaSelecionadaId] = useState('')
  const [novoAnoMeta, setNovoAnoMeta] = useState(new Date().getFullYear())
  const [novoValorSonho, setNovoValorSonho] = useState('')
  const [salvandoVinculo, setSalvandoVinculo] = useState(false)

  const carregarConta = useCallback(() => {
    if (!clienteId || !contaId) return
    setLoadingConta(true)
    listarContasBancarias(clienteId)
      .then(lista => {
        setContasBancarias(lista)
        setConta(lista.find(c => c.id === contaId) ?? null)
      })
      .catch(() => setMsg('Erro ao carregar dados da conta.'))
      .finally(() => setLoadingConta(false))
  }, [clienteId, contaId])

  useEffect(() => { carregarConta() }, [carregarConta])

  const carregarExtrato = useCallback(async () => {
    if (!contaId) return
    setLoadingExtrato(true)
    try {
      setLancamentos(await obterExtratoConta(contaId, de, ate))
    } catch {
      setMsg('Erro ao carregar extrato.')
    } finally {
      setLoadingExtrato(false)
    }
  }, [contaId, de, ate])

  useEffect(() => { carregarExtrato() }, [carregarExtrato])

  useEffect(() => { if (clienteId) listarCategorias(clienteId).then(setCategorias).catch(() => {}) }, [clienteId])
  useEffect(() => { if (clienteId) listarTransferencias(clienteId).then(setTransferencias).catch(() => {}) }, [clienteId])
  // Bloco 5 (Money Vision): gráfico "saldo mês a mês" só pra conta de investimento.
  useEffect(() => {
    if (!contaId || conta?.tipo !== 'Investimento') { setHistoricoSaldo([]); return }
    obterHistoricoSaldoMensal(contaId, 12).then(setHistoricoSaldo).catch(() => {})
  }, [contaId, conta?.tipo])

  const carregarPendencias = useCallback(() => {
    if (!contaId) return
    setLoadingPendencias(true)
    obterPendenciasConta(contaId)
      .then(p => { setRecebiveis(p.recebiveis); setPagamentos(p.pagamentos) })
      .catch(() => setMsg('Erro ao carregar pendências.'))
      .finally(() => setLoadingPendencias(false))
  }, [contaId])

  useEffect(() => { carregarPendencias() }, [carregarPendencias])

  async function handleArquivoSelecionado(arquivo: File) {
    if (!contaId) return
    setMsg('')
    setPreviewCarregando(true)
    setArquivoImportar(arquivo)
    setResultadoImportacao(null)
    setResolucoesDuplicatas({})
    setSugestoesConfirmadas(false)
    // Cartão: cada arquivo é uma fatura nova — reseta os campos da fatura anterior.
    setBancoFatura('')
    setDataVencimentoFatura('')
    setDataFechamentoFatura('')
    setCompetenciaFatura('')
    setCompetenciaEditadaManualmente(false)
    setInverterSinaisManualmente(undefined)
    try {
      const resumo = await previewExtrato(contaId, arquivo)
      setDataInicioImport(resumo.dataInicioArquivo || todayISO())
      setDataFimImport(resumo.dataFimArquivo || todayISO())
      setResumoImportacao(resumo)
      setModalImportar(true)
      if (conta?.tipo === 'CartaoCredito') {
        listarBancosCartao(contaId).then(setBancosDisponiveis).catch(() => {})
      }
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao ler o arquivo.')
    } finally {
      setPreviewCarregando(false)
    }
  }

  // Cartão: competência sugerida pelo vencimento ("yyyy-MM"), mas o usuário pode sobrescrever —
  // depois disso a sugestão automática para de pisar no valor editado.
  function handleAlterarVencimentoFatura(valor: string) {
    setDataVencimentoFatura(valor)
    if (!competenciaEditadaManualmente && valor) setCompetenciaFatura(valor.slice(0, 7))
  }

  async function handleAlterarIntervaloImportacao(novoInicio: string, novoFim: string) {
    setDataInicioImport(novoInicio)
    setDataFimImport(novoFim)
    if (!contaId || !arquivoImportar || !novoInicio || !novoFim) return
    setPreviewCarregando(true)
    try {
      setResolucoesDuplicatas({})
      setResumoImportacao(await previewExtrato(contaId, arquivoImportar, {
        dataInicio: novoInicio, dataFim: novoFim, inverterSinaisManualmente,
      }))
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao calcular resumo da importação.')
    } finally {
      setPreviewCarregando(false)
    }
  }

  async function handleAlterarInverterSinais(valor: boolean | undefined) {
    setInverterSinaisManualmente(valor)
    if (!contaId || !arquivoImportar) return
    setPreviewCarregando(true)
    try {
      setResumoImportacao(await previewExtrato(contaId, arquivoImportar, {
        dataInicio: dataInicioImport, dataFim: dataFimImport, inverterSinaisManualmente: valor,
      }))
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao recalcular o resumo da importação.')
    } finally {
      setPreviewCarregando(false)
    }
  }

  async function handleConfirmarImportacao() {
    if (!contaId || !arquivoImportar) return
    setImportando(true)
    setMsg('')
    try {
      // Só manda as que o usuário trocou do padrão — índice ausente já é Mesclar no backend.
      const resolucoes: ResolucaoDuplicata[] = Object.entries(resolucoesDuplicatas).map(([indice, acao]) => ({
        transacaoIndice: Number(indice), acao,
      }))
      const resultado = await importarExtrato(contaId, arquivoImportar, {
        dataInicio: dataInicioImport,
        dataFim: dataFimImport,
        resolucoesDuplicatas: resolucoes,
        inverterSinaisManualmente,
        ...(conta?.tipo === 'CartaoCredito' ? {
          banco: bancoFatura,
          dataVencimentoFatura,
          dataFechamentoFatura: dataFechamentoFatura || undefined,
          competenciaFatura: competenciaFatura || undefined,
        } : {}),
      })
      setResultadoImportacao(resultado)
      setModalImportar(false)
      setArquivoImportar(null)
      setResumoImportacao(null)
      setResolucoesDuplicatas({})
      setInverterSinaisManualmente(undefined)
      carregarConta()
      carregarExtrato()
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao importar extrato.')
    } finally {
      setImportando(false)
    }
  }

  // Item 3.3: limiar de confiança pra "Confirmar todas" vincular sem revisão manual — abaixo
  // disso, fica pra revisão individual (tela de Contas já mostra Vincular/Ignorar por sugestão).
  const CONFIANCA_MINIMA_CONFIRMAR_TODAS = 80

  // Fase 1.2/1.7: "confirmar todas" do resumo pós-importação — vincula de uma vez cada sugestão
  // (título pendente × lançamento real) que o motor encontrou no intervalo do próprio arquivo.
  async function handleConfirmarTodasSugestoes() {
    if (!contaId || !clienteId) return
    setConfirmandoTodasSugestoes(true)
    setMsg('')
    try {
      const sugestoes = await listarSugestoesVinculo(clienteId, dataInicioImport || todayISO(), dataFimImport || todayISO(), contaId)
      const altaConfianca = sugestoes.filter(s => s.score >= CONFIANCA_MINIMA_CONFIRMAR_TODAS)
      for (const s of altaConfianca) {
        await atualizarContaProvisionada(clienteId, s.contaProvisionadaId, {
          pago: true, contaBancariaId: s.contaBancariaId, dataPagamento: s.lancamentoData, lancamentoVinculadoId: s.lancamentoId,
        })
      }
      const restantes = sugestoes.length - altaConfianca.length
      setMsg(restantes > 0
        ? `${altaConfianca.length} vínculo(s) confirmado(s). ${restantes} com confiança abaixo de ${CONFIANCA_MINIMA_CONFIRMAR_TODAS}% — revise manualmente em Contas.`
        : '')
      setSugestoesConfirmadas(true)
      carregarConta()
      carregarPendencias()
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao confirmar as sugestões de vínculo.')
    } finally {
      setConfirmandoTodasSugestoes(false)
    }
  }

  async function handleDesfazerTransferencia(transferenciaId: string) {
    if (!confirm('Desfazer esta classificação como Transferência? Se a contrapartida na outra conta veio de um lançamento real (extrato já importado), ela volta a ficar sem categoria, pendente — o dinheiro continua lançado, só a classificação muda. Se ela foi criada automaticamente só pra essa transferência e nunca foi confirmada pelo extrato, ela é removida e os saldos das duas contas são recalculados.')) return
    setMsg('')
    try {
      await desfazerClassificacaoTransferencia(transferenciaId)
      carregarConta()
      carregarExtrato()
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao desfazer classificação.')
    }
  }

  async function abrirRevisarDuplicatas() {
    if (!contaId) return
    setModalDuplicatas(true)
    setCarregandoDuplicatas(true)
    try {
      setDuplicatas(await listarDuplicatas(contaId))
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao buscar duplicatas.')
    } finally {
      setCarregandoDuplicatas(false)
    }
  }

  // Item 3.2: "Manter os dois" — grava a decisão no backend; o par não reaparece mais mesmo
  // depois de recarregar a tela.
  async function manterOsDoisDaDuplicata(dup: DuplicataProvavel) {
    if (!contaId) return
    setExcluindoDuplicataId(dup.lancamentoAId)
    setMsg('')
    try {
      await manterDuplicata(contaId, dup.lancamentoAId, dup.lancamentoBId)
      setDuplicatas(prev => prev.filter(d => d !== dup))
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao gravar a decisão.')
    } finally {
      setExcluindoDuplicataId(null)
    }
  }

  // Full reload (não só a lista local): a duplicata pode estar em qualquer data do histórico,
  // fora do intervalo que o extrato visível na tela está mostrando agora.
  async function excluirLadoDaDuplicata(dup: DuplicataProvavel, lado: 'A' | 'B') {
    if (!contaId) return
    const id = lado === 'A' ? dup.lancamentoAId : dup.lancamentoBId
    const data = lado === 'A' ? dup.dataA : dup.dataB
    const descricao = lado === 'A' ? dup.descricaoA : dup.descricaoB
    if (!confirm(`Excluir "${descricao}"? Essa ação não pode ser desfeita.`)) return
    setExcluindoDuplicataId(id)
    setMsg('')
    try {
      await excluirLancamento(contaId, { id, data })
      setDuplicatas(prev => prev.filter(d => d !== dup))
      carregarConta()
      carregarExtrato()
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao excluir lançamento.')
    } finally {
      setExcluindoDuplicataId(null)
    }
  }

  function abrirConfirmarExclusao(lancamento: LancamentoExtrato) {
    setExcluirConfirmacao(lancamento)
    setMsg('')
  }

  async function confirmarExclusao() {
    const lancamento = excluirConfirmacao
    if (!lancamento?.id || !contaId) return
    setExcluindoLancamento(true)
    setMsg('')
    try {
      const resultado = await excluirLancamento(contaId, { id: lancamento.id, data: lancamento.data })
      if (resultado.tituloReaberto) setMsg(`Lançamento excluído. O título "${resultado.tituloReaberto}" voltou a ficar em aberto.`)
      // Transferência tem contrapartida numa OUTRA conta, fora do alcance desta tela — só nesse
      // caso um reload completo compensa; nos demais, ajustar a lista local evita recarregar tudo.
      if (lancamento.categoria === 'Transferência') {
        carregarConta()
        carregarExtrato()
      } else {
        const idx = lancamentos.findIndex(l => l.id === lancamento.id && l.data === lancamento.data)
        if (idx !== -1) {
          const valorRemovido = lancamentos[idx].valor
          // Cada linha anterior a esta na lista (mais recente, já que a ordenação é decrescente)
          // teve seu saldo acumulado somado considerando esse valor — precisa descontar.
          const restantes = lancamentos
            .map((l, i) => i < idx ? { ...l, saldoAcumulado: l.saldoAcumulado - valorRemovido } : l)
            .filter((_, i) => i !== idx)
          setLancamentos(restantes)
          setConta(prev => prev ? { ...prev, saldoAtual: restantes[0]?.saldoAcumulado ?? prev.saldoInicial } : prev)
        }
      }
      carregarPendencias()
      setExcluirConfirmacao(null)
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao excluir lançamento.')
    } finally {
      setExcluindoLancamento(false)
    }
  }

  function handleCategoriaCriada(nova: CategoriaAdmin) {
    const item = { nome: nova.nome, tipoCusto: nova.tipo, grupo: nova.grupoNome }
    // ehEntrada/ehSaida vêm prontos do backend (CategoriaService) — mesma regra usada em
    // ClientExtratoRevisaoPage, pra não duplicar essa partição aqui de novo.
    if (nova.ehEntrada) setCategorias(prev => ({ ...prev, entradas: [...prev.entradas, item] }))
    if (nova.ehSaida) setCategorias(prev => ({ ...prev, saidas: [...prev.saidas, item] }))
  }

  function abrirModalEditarCategoria(lancamento: LancamentoExtrato) {
    setLancamentoParaEditar(lancamento)
    setCategoriaEdicao(lancamento.categoria ?? '')
    setMsg('')
  }

  // Núcleo da troca de categoria — usado tanto pelo painel (✏️) quanto pelo chip clicável
  // direto na linha, pra não duplicar a lógica de regra/estado local nos dois caminhos.
  async function aplicarNovaCategoria(lancamento: LancamentoExtrato, novaCategoria: string) {
    if (!lancamento.id || !contaId || !novaCategoria) return
    setMsg('')
    await categorizarPendentes(contaId, [{ id: lancamento.id, data: lancamento.data, categoria: novaCategoria }])

    // Categoria veio de uma regra e o usuário trocou pra outra — a regra em si NUNCA é alterada
    // sozinha, mas oferece atualizar (só essa reclassificação manual, não muda o critério).
    const regraId = lancamento.regraCategorizacaoId
    if (regraId && novaCategoria !== lancamento.categoria && clienteId) {
      try {
        const regras = await listarRegras(clienteId)
        const regra = regras.find(r => r.id === regraId)
        if (regra && regra.acaoTipo === 'Categoria' && regra.categoria !== novaCategoria
          && confirm(`Esse lançamento veio da regra baseada em "${regra.descricaoReferencia}" (categoria "${regra.categoria}"). Atualizar a regra para usar "${novaCategoria}" daqui pra frente?`)) {
          await atualizarRegra(regra.id, {
            descricaoReferencia: regra.descricaoReferencia,
            acaoTipo: regra.acaoTipo,
            categoria: novaCategoria,
            ativa: regra.ativa,
          })
        }
      } catch {
        // Não bloqueia a edição do lançamento (já salva) por causa disso.
      }
    }

    // Trocar categoria não move dinheiro (saldo/saldoAcumulado ficam intactos) — só a linha
    // em si precisa refletir a categoria nova, sem recarregar conta/extrato inteiros.
    setLancamentos(prev => prev.map(l =>
      (l.id === lancamento.id && l.data === lancamento.data) ? { ...l, categoria: novaCategoria } : l))

    // Item 8, Bloco 2: lançamento categorizado manualmente (não veio de regra) — se existem outros
    // parecidos, oferece aplicar a todos de uma vez e criar uma regra pras próximas importações.
    if (!regraId) {
      const tipo = lancamento.valor >= 0 ? 'Entrada' : 'Saida'
      try {
        const contagem = await contarCorrespondencias(contaId, tipo, lancamento.descricao)
        if (contagem > 0) setOfertaRegra({ lancamento, categoria: novaCategoria, contagem })
      } catch {
        // Não bloqueia a edição por causa disso — só não oferece a regra.
      }
    }
  }

  async function confirmarOfertaRegra() {
    if (!ofertaRegra || !contaId || !clienteId) return
    setCriandoOfertaRegra(true)
    setMsg('')
    try {
      const tipo = ofertaRegra.lancamento.valor >= 0 ? 'Entrada' : 'Saida'
      const regra = await criarRegra(clienteId, {
        contaBancariaId: contaId,
        tipo,
        descricaoReferencia: ofertaRegra.lancamento.descricao,
        acaoTipo: 'Categoria',
        categoria: ofertaRegra.categoria,
      })
      const total = await aplicarRegraRetroativamente(regra.id)
      setMsg(`Regra criada! ${total} lançamento(s) parecido(s) foram categorizados como "${ofertaRegra.categoria}".`)
      setOfertaRegra(null)
      carregarExtrato()
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao criar regra.')
    } finally {
      setCriandoOfertaRegra(false)
    }
  }

  async function confirmarEdicaoCategoria() {
    if (!lancamentoParaEditar || !categoriaEdicao) return
    setSalvandoEdicao(true)
    try {
      await aplicarNovaCategoria(lancamentoParaEditar, categoriaEdicao)
      setLancamentoParaEditar(null)
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao editar categoria.')
    } finally {
      setSalvandoEdicao(false)
    }
  }

  async function selecionarCategoriaViaChip(lancamento: LancamentoExtrato, novaCategoria: string) {
    setSalvandoChip(true)
    try {
      await aplicarNovaCategoria(lancamento, novaCategoria)
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao editar categoria.')
    } finally {
      setSalvandoChip(false)
      setChipAbertoKey(null)
    }
  }

  function abrirModalTransferencia(lancamento: LancamentoExtrato) {
    setLancamentoParaTransferencia(lancamento)
    setContaContrapartidaId('')
    setCandidatoContrapartida(null)
    setMsg('')
  }

  useEffect(() => {
    if (!lancamentoParaTransferencia || !contaContrapartidaId) { setCandidatoContrapartida(null); return }
    let cancelado = false
    const tipoOriginal = lancamentoParaTransferencia.valor >= 0 ? 'Entrada' : 'Saida'
    setBuscandoCandidato(true)
    buscarCandidatoContrapartida(contaContrapartidaId, lancamentoParaTransferencia.data, Math.abs(lancamentoParaTransferencia.valor), tipoOriginal)
      .then(candidato => { if (!cancelado) setCandidatoContrapartida(candidato) })
      .finally(() => { if (!cancelado) setBuscandoCandidato(false) })
    return () => { cancelado = true }
  }, [lancamentoParaTransferencia, contaContrapartidaId])

  async function confirmarTransferencia(vincular: boolean) {
    if (!lancamentoParaTransferencia?.id || !contaId || !contaContrapartidaId) return
    setConvertendo(true)
    setMsg('')
    try {
      await converterLancamentoEmTransferencia({
        contaId, lancamentoId: lancamentoParaTransferencia.id, data: lancamentoParaTransferencia.data,
        tipo: lancamentoParaTransferencia.valor >= 0 ? 'Entrada' : 'Saida',
        contaContrapartidaId,
        lancamentoContrapartidaId: vincular && candidatoContrapartida?.id ? candidatoContrapartida.id : undefined,
        dataContrapartida: vincular ? candidatoContrapartida?.data : undefined,
      })
      setLancamentoParaTransferencia(null)
      carregarConta()
      carregarExtrato()
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao converter em transferência.')
    } finally {
      setConvertendo(false)
    }
  }

  function abrirModalRendimento() {
    setModoSaldo(true)
    setSaldoBancoDisplay(''); setSaldoBanco(0)
    setRendValorDisplay(''); setRendValor(0); setRendPositivo(true); setRendData(todayISO()); setRendDescricao('')
    setModalRendimento(true)
  }

  // Bloco 5 (Money Vision): no modo "saldo", a diferença entre o que o cliente digitou e o saldo
  // atual É o rendimento (ou perda) — o cliente nunca precisa calcular isso de cabeça.
  const diferencaSaldo = modoSaldo ? saldoBanco - (conta?.saldoAtual ?? 0) : null

  async function handleSalvarRendimento() {
    if (!contaId) return
    const valorFinal = modoSaldo ? diferencaSaldo! : (rendPositivo ? rendValor : -rendValor)
    if (!valorFinal) return
    setSalvandoRendimento(true)
    try {
      await registrarRendimento(contaId, {
        data: rendData,
        valor: valorFinal,
        descricao: rendDescricao || (valorFinal < 0 ? 'Ajuste negativo (IR/IOF/oscilação)' : undefined),
      })
      setModalRendimento(false)
      carregarConta()
      carregarExtrato()
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao registrar rendimento.')
    } finally {
      setSalvandoRendimento(false)
    }
  }

  function abrirModalConferirSaldo() {
    setConferirData(todayISO()); setConferirSaldoDisplay(''); setConferirSaldoReal(0)
    setModalConferirSaldo(true)
  }

  async function handleConferirSaldo() {
    if (!contaId) return
    setSalvandoConferirSaldo(true)
    try {
      await ajustarSaldo(contaId, { data: conferirData, saldoReal: conferirSaldoReal })
      setModalConferirSaldo(false)
      carregarConta()
      carregarExtrato()
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao conferir saldo.')
    } finally {
      setSalvandoConferirSaldo(false)
    }
  }

  function abrirModalVincular() {
    setMetaSelecionadaId(''); setNovoAnoMeta(new Date().getFullYear()); setNovoValorSonho('')
    if (clienteId) listarMetas(clienteId).then(setMetasDisponiveis).catch(() => {})
    setModalVincular(true)
  }

  async function handleVincularExistente() {
    if (!contaId || !metaSelecionadaId) return
    setSalvandoVinculo(true)
    try {
      await vincularMeta(contaId, metaSelecionadaId)
      setModalVincular(false)
      carregarConta()
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao vincular meta.')
    } finally {
      setSalvandoVinculo(false)
    }
  }

  async function handleCriarEVincular() {
    if (!contaId || !clienteId) return
    const valorSonho = parseFloat(novoValorSonho.replace(/\./g, '').replace(',', '.')) || 0
    if (valorSonho <= 0) return
    setSalvandoVinculo(true)
    try {
      const novaMeta = await salvarMeta({
        clienteId, ano: novoAnoMeta, metaReceita: 0, metaLucro: 0, mesInicio: 1, periodoMeses: 12,
        modoMeta: 'metodo', valorSonho,
      })
      await vincularMeta(contaId, novaMeta.id)
      setModalVincular(false)
      carregarConta()
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao criar/vincular meta.')
    } finally {
      setSalvandoVinculo(false)
    }
  }

  async function handleDesvincular(metaId: string) {
    if (!contaId) return
    if (!confirm('Desvincular esta meta da conta? O progresso volta a ser o valor manual anterior.')) return
    try {
      await desvincularMeta(contaId, metaId)
      carregarConta()
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao desvincular meta.')
    }
  }

  // Bloco 5 (Money Vision): dias desde a última atualização de saldo — vira aviso quando > 30.
  const diasDesdeUltimaAtualizacao = conta?.ultimaAtualizacaoRendimento
    ? Math.floor((new Date(todayISO()).getTime() - new Date(conta.ultimaAtualizacaoRendimento).getTime()) / 86400000)
    : null

  // Tipo de categoria (Receita/CustoFixo/CustoVariavel/.../Investimento) por nome — pra colorir o
  // chip do extrato sem duplicar a classificação que o backend já devolve em `categorias`.
  const tipoPorCategoria = useMemo(
    () => new Map([...categorias.entradas, ...categorias.saidas].map(c => [c.nome, c.tipoCusto])),
    [categorias],
  )

  type TipoChip = 'receita' | 'despesa' | 'transferencia' | 'investimento' | 'sem-categoria' | 'ajuste'
  function classificarChip(l: LancamentoExtrato): TipoChip {
    // Bloco 3: ajuste de saldo vem antes de tudo — mesmo categorizado (ex.: "Devoluções" por
    // engano antes desta correção), nunca é receita/despesa/transferência de verdade.
    if (l.tipoCusto === 'AjusteSaldo') return 'ajuste'
    if (l.pendenteCategorizacao || !l.categoria) return 'sem-categoria'
    if (l.categoria === 'Transferência') return 'transferencia'
    if (tipoPorCategoria.get(l.categoria) === 'Investimento') return 'investimento'
    return l.valor >= 0 ? 'receita' : 'despesa'
  }

  // Bloco 4, item 1: o chip de transferência mostra a OUTRA conta ("⇄ Investimento Nubank"), não
  // o literal "Transferência" — olha nas Transferencias do cliente qual é a ponta que não é esta conta.
  const nomeOutraContaPorTransferenciaId = useMemo(() => {
    const mapa = new Map<string, string>()
    if (!conta) return mapa
    for (const t of transferencias) {
      if (t.contaOrigemId === conta.id) mapa.set(t.id, t.contaDestinoNome)
      else if (t.contaDestinoId === conta.id) mapa.set(t.id, t.contaOrigemNome)
    }
    return mapa
  }, [transferencias, conta])

  function rotuloChip(l: LancamentoExtrato, tipo: TipoChip): string {
    if (tipo === 'ajuste') return 'Ajuste de saldo'
    if (tipo === 'sem-categoria') return 'Sem categoria'
    if (tipo === 'transferencia') {
      const outraConta = l.transferenciaId ? nomeOutraContaPorTransferenciaId.get(l.transferenciaId) : undefined
      return outraConta ? `⇄ ${outraConta}` : 'Transferência'
    }
    return l.categoria || '—'
  }

  // Barra de filtros (item 6, Bloco 2) — nomes únicos pro dropdown de categoria.
  const categoriasNomes = useMemo(
    () => [...new Set([...categorias.entradas, ...categorias.saidas].map(c => c.nome))].sort(),
    [categorias],
  )

  const lancamentosFiltrados = useMemo(() => {
    const texto = filtroTexto.trim().toLowerCase()
    return lancamentos.filter(l => {
      if (texto && !l.descricao.toLowerCase().includes(texto)) return false
      if (filtroTipo === 'entrada' && l.valor < 0) return false
      if (filtroTipo === 'saida' && l.valor >= 0) return false
      if (filtroCategoria && l.categoria !== filtroCategoria) return false
      if (filtroSoSemCategoria && !(l.pendenteCategorizacao || !l.categoria)) return false
      if (filtroSoTransferenciaInvestimento) {
        const tipo = classificarChip(l)
        if (tipo !== 'transferencia' && tipo !== 'investimento') return false
      }
      return true
    })
  }, [lancamentos, filtroTexto, filtroTipo, filtroCategoria, filtroSoSemCategoria, filtroSoTransferenciaInvestimento, tipoPorCategoria])

  // Saldo de fim de dia vem sempre do extrato COMPLETO (nunca do filtrado) — filtrar a visão não
  // pode fazer parecer que o saldo real de um dia mudou. Primeiro encontrado = mais recente do
  // dia, já que `lancamentos` vem ordenado assim (ver comentário de ObterExtratoAsync).
  const saldoPorDia = useMemo(() => {
    const mapa = new Map<string, number>()
    for (const l of lancamentos) if (!mapa.has(l.data)) mapa.set(l.data, l.saldoAcumulado)
    return mapa
  }, [lancamentos])

  // Agrupamento por dia (item 4, Bloco 1).
  interface GrupoDia { data: string; itens: LancamentoExtrato[]; entradas: number; saidas: number; saldoDoDia: number }
  const gruposPorDia = useMemo<GrupoDia[]>(() => {
    const porData = new Map<string, LancamentoExtrato[]>()
    for (const l of lancamentosFiltrados) {
      const grupo = porData.get(l.data)
      if (grupo) grupo.push(l)
      else porData.set(l.data, [l])
    }
    return [...porData.entries()].map(([data, itens]) => {
      // Ajuste de saldo não é uma transação de verdade — não conta no "Entradas +X · Saídas −Y"
      // do dia (item 10, Bloco 3), mas o Saldo do dia continua o real (vem de saldoPorDia).
      const itensReais = itens.filter(l => l.tipoCusto !== 'AjusteSaldo')
      return {
        data,
        itens,
        entradas: itensReais.filter(l => l.valor >= 0).reduce((s, l) => s + l.valor, 0),
        saidas: itensReais.filter(l => l.valor < 0).reduce((s, l) => s - l.valor, 0),
        saldoDoDia: saldoPorDia.get(data) ?? itens[0].saldoAcumulado,
      }
    })
  }, [lancamentosFiltrados, saldoPorDia])

  // Seleção em lote (item 7, Bloco 2).
  function chaveLancamento(l: LancamentoExtrato): string { return `${l.data}-${l.id}` }
  function toggleSelecao(l: LancamentoExtrato) {
    if (!l.id) return
    const chave = chaveLancamento(l)
    setSelecionados(prev => {
      const next = new Set(prev)
      if (next.has(chave)) next.delete(chave)
      else next.add(chave)
      return next
    })
  }
  const itensSelecionados = useMemo(
    () => lancamentos.filter(l => l.id && selecionados.has(chaveLancamento(l))),
    [lancamentos, selecionados],
  )
  function limparSelecao() { setSelecionados(new Set()) }

  async function confirmarLoteCategoria() {
    if (!contaId || !loteCategoriaValor || itensSelecionados.length === 0) return
    setProcessandoLote(true)
    setMsg('')
    try {
      await categorizarPendentes(contaId, itensSelecionados.map(l => ({ id: l.id!, data: l.data, categoria: loteCategoriaValor })))
      setLancamentos(prev => prev.map(l => (l.id && selecionados.has(chaveLancamento(l))) ? { ...l, categoria: loteCategoriaValor } : l))
      setMsg(`${itensSelecionados.length} lançamento(s) categorizado(s) como "${loteCategoriaValor}".`)
      setLoteCategoriaAberta(false)
      setLoteCategoriaValor('')
      limparSelecao()
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao aplicar categoria em lote.')
    } finally {
      setProcessandoLote(false)
    }
  }

  async function confirmarLoteTransferencia() {
    if (!contaId || !loteContaContrapartidaId || itensSelecionados.length === 0) return
    setProcessandoLote(true)
    setMsg('')
    try {
      for (const l of itensSelecionados) {
        if (!l.id) continue
        await converterLancamentoEmTransferencia({
          contaId, lancamentoId: l.id, data: l.data,
          tipo: l.valor >= 0 ? 'Entrada' : 'Saida',
          contaContrapartidaId: loteContaContrapartidaId,
        })
      }
      setMsg(`${itensSelecionados.length} lançamento(s) marcado(s) como transferência.`)
      setLoteTransferenciaAberta(false)
      setLoteContaContrapartidaId('')
      limparSelecao()
      carregarConta()
      carregarExtrato()
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao marcar lote como transferência.')
    } finally {
      setProcessandoLote(false)
    }
  }

  async function confirmarLoteExcluir() {
    if (!contaId || itensSelecionados.length === 0) return
    setProcessandoLote(true)
    setMsg('')
    try {
      const itens = itensSelecionados.filter(l => l.id).map(l => ({ id: l.id!, data: l.data }))
      const resultado = await excluirLote(contaId, itens)
      setMsg(
        `${resultado.totalExcluidos} lançamento(s) excluído(s).`
        + (resultado.titulosReabertos.length > 0 ? ` ${resultado.titulosReabertos.length} título(s) voltaram a ficar em aberto.` : ''),
      )
      setLoteExcluirAberta(false)
      limparSelecao()
      carregarConta()
      carregarExtrato()
      carregarPendencias()
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao excluir em lote.')
    } finally {
      setProcessandoLote(false)
    }
  }

  // Coluna de ações (item 1, Bloco 1) — ícones de 28px à esquerda da linha, substituindo os
  // antigos links de texto embaixo da descrição. ✏️ funciona pra qualquer linha com id (inclusive
  // pendente — era bloqueado antes, mas é exatamente como ela deixa de estar pendente).
  function renderAcoesLinha(l: LancamentoExtrato) {
    if (!l.id) return <span className="cd-acoes-linha" aria-hidden="true" />
    const ehTransferencia = l.categoria === 'Transferência'
    // Bloco 3: ajuste de saldo não é uma transação de verdade — não faz sentido trocar categoria
    // nem virar transferência, só excluir (se foi lançado errado).
    const ehAjuste = l.tipoCusto === 'AjusteSaldo'
    return (
      <span className="cd-acoes-linha">
        <button
          type="button"
          className="cd-acao-btn"
          aria-label={`Editar categoria de "${l.descricao}"`}
          title={ehAjuste ? 'Ajuste de saldo não é reclassificável' : 'Editar categoria'}
          onClick={() => abrirModalEditarCategoria(l)}
          disabled={ehAjuste}
        >
          ✏️
        </button>
        {ehTransferencia ? (
          <button
            type="button"
            className="cd-acao-btn"
            aria-label={`Desfazer transferência de "${l.descricao}"`}
            title="Desfazer classificação de transferência"
            onClick={() => l.transferenciaId && handleDesfazerTransferencia(l.transferenciaId)}
          >
            ↩
          </button>
        ) : (
          <button
            type="button"
            className="cd-acao-btn"
            aria-label={`Marcar "${l.descricao}" como transferência`}
            title={ehAjuste ? 'Ajuste de saldo não é reclassificável' : 'Marcar como transferência'}
            onClick={() => abrirModalTransferencia(l)}
            disabled={ehAjuste}
          >
            ⇄
          </button>
        )}
        <button
          type="button"
          className="cd-acao-btn cd-acao-excluir"
          aria-label={`Excluir lançamento ${l.descricao}`}
          title="Excluir lançamento"
          onClick={() => abrirConfirmarExclusao(l)}
        >
          🗑
        </button>
      </span>
    )
  }

  // Chip da categoria (item 2 e 5, Bloco 1): clicar abre o combobox ali mesmo e salva ao escolher
  // — transferência não é reclassificável por aqui (teria que desvincular o par primeiro).
  function renderChip(l: LancamentoExtrato, chave: string) {
    const tipo = classificarChip(l)
    if (chipAbertoKey === chave) {
      return (
        <span className="cd-chip-combobox" onClick={e => e.stopPropagation()}>
          <CategoriaCombobox
            clienteId={clienteId}
            categorias={l.valor >= 0 ? categorias.entradas : categorias.saidas}
            value={l.categoria ?? ''}
            onChange={nome => selecionarCategoriaViaChip(l, nome)}
            onCategoriaCriada={handleCategoriaCriada}
            blocoPadraoNovaCategoria={l.valor >= 0 ? 'RECEITAS OPERACIONAIS' : 'DESPESAS OPERACIONAIS'}
            placeholder="Categoria..."
          />
        </span>
      )
    }
    const naoClicavel = tipo === 'transferencia' || tipo === 'ajuste'
    return (
      <button
        type="button"
        className={`cd-chip cd-chip-${tipo}`}
        onClick={() => !naoClicavel && setChipAbertoKey(chave)}
        disabled={salvandoChip || naoClicavel}
      >
        {rotuloChip(l, tipo)}
        {l.regraCategorizacaoId && <span title="Categorizado automaticamente por uma regra"> ⚙️</span>}
      </button>
    )
  }

  const renderPendencia = (item: ContaProvisionada, tipo: 'receber' | 'pagar') => {
    const diasAtraso = calcularDiasAtraso(item.dataVencimento, item.pago)
    return (
      <div key={`${item.descricao}-${item.dataVencimento}-${item.valor}`} className={`conta-item ${diasAtraso > 0 ? 'vencida' : ''}`}>
        <div className="conta-info">
          <div className="conta-desc">{item.descricao}</div>
          <div className="conta-meta">
            {item.dataVencimento ? `Vence: ${fmtDate(item.dataVencimento)}` : 'Sem vencimento'}
            {item.categoria ? ` · ${item.categoria}` : ''}
            {diasAtraso > 0 && <span className="conta-atrasada"> · atrasada há {diasAtraso} dia{diasAtraso > 1 ? 's' : ''}</span>}
          </div>
        </div>
        <div className={`conta-valor ${tipo}`}>{fmtBRL(item.valor)}</div>
      </div>
    )
  }

  if (loadingConta) return <p style={{ color: 'var(--tx3)' }}>Carregando...</p>

  if (!conta) {
    return (
      <div className="cd-vazio">
        <p>Conta bancária não encontrada.</p>
        <button className="cd-btn-voltar" onClick={() => navigate('/banco')}>← Voltar</button>
      </div>
    )
  }

  return (
    <>
      <div className="cd-header">
        <div>
          <button className="cd-btn-voltar" onClick={() => navigate('/banco')}>← Voltar</button>
          <h2 className="cd-titulo">{conta.nome}</h2>
          <div className="cd-subtitulo">{TIPO_LABEL[conta.tipo] ?? conta.tipo}</div>
        </div>
        <div className="cd-saldo-box">
          {/* Cartão: dívida é sempre vermelha — usa a soma das Faturas reais não pagas (item 4,
              Money Vision), não saldoDevedor (ledger inteiro, pode zerar com um pagamento nunca
              vinculado a nenhuma fatura) nem saldoAtual (sinal pode ficar invertido). */}
          <span className="cd-saldo-label">{conta.tipo === 'CartaoCredito' ? 'Fatura(s) em aberto' : 'Saldo atual'}</span>
          <span className={`cd-saldo-val ${conta.tipo === 'CartaoCredito' ? 'val-red' : conta.saldoAtual < 0 ? 'val-red' : 'val-green'}`}>
            {fmtBRL(conta.tipo === 'CartaoCredito' ? (conta.faturasEmAberto ?? conta.saldoDevedor ?? 0) : conta.saldoAtual)}
          </span>
        </div>
      </div>

      {conta.pendentesCategorizacao > 0 && (
        <div className="cd-msg cd-msg-aviso" style={{ cursor: 'pointer' }} onClick={() => navigate(`/banco/extrato/${contaId}`)}>
          🏷️ {conta.pendentesCategorizacao} lançamento(s) aguardando categoria — clique para categorizar
        </div>
      )}

      <div className="cd-acoes">
        <input
          ref={fileInputRef}
          type="file"
          accept=".ofx,.csv,.xlsx"
          style={{ display: 'none' }}
          onChange={e => {
            const f = e.target.files?.[0]
            if (f) handleArquivoSelecionado(f)
            e.target.value = ''
          }}
        />
        <button className="cd-btn-importar" onClick={() => fileInputRef.current?.click()} disabled={previewCarregando}>
          {previewCarregando ? 'Lendo arquivo...' : '⬆️ Importar extrato'}
        </button>
        <button className="cd-btn-importar" onClick={abrirRevisarDuplicatas}>
          🔍 Revisar duplicatas
        </button>
        {conta.tipo === 'Investimento' && (
          <button className="cd-btn-importar" onClick={abrirModalRendimento}>
            📈 Atualizar saldo
          </button>
        )}
        {conta.tipo !== 'Investimento' && (
          <button className="cd-btn-importar" onClick={abrirModalConferirSaldo}>
            ⚖️ Conferir saldo
          </button>
        )}
        {conta.tipo === 'CartaoCredito' && (
          <button className="cd-btn-importar" onClick={() => navigate(`/banco/${contaId}/faturas`)}>
            💳 Ver faturas
          </button>
        )}
      </div>

      {resultadoImportacao && (
        <div className="cd-msg cd-msg-sucesso">
          ✅ {resultadoImportacao.totalImportadas} lançamento(s) importado(s)
          {resultadoImportacao.totalConciliadasTransferencia > 0 && (
            <> — {resultadoImportacao.totalConciliadasTransferencia} conciliado(s) com transferência(s) já classificada(s)</>
          )}
          {resultadoImportacao.totalAmbiguasTransferencia > 0 && (
            <> — {resultadoImportacao.totalAmbiguasTransferencia} com mais de uma transferência pendente parecida (confira manualmente)</>
          )}
          {resultadoImportacao.totalCategorizadasPorRegra > 0 && (
            <> — {resultadoImportacao.totalCategorizadasPorRegra} categorizado(s) automaticamente por regra</>
          )}
          {resultadoImportacao.totalPendentesCategorizacao > 0 && (
            <> — {resultadoImportacao.totalPendentesCategorizacao} sem categoria sugerida (
              <button
                type="button"
                onClick={() => navigate(`/banco/extrato/${contaId}`)}
                style={{ background: 'none', border: 'none', color: 'var(--accent)', cursor: 'pointer', padding: 0, font: 'inherit', textDecoration: 'underline' }}
              >
                categorizar agora
              </button>
              )
            </>
          )}
          {resultadoImportacao.totalMescladasComManual > 0 && (
            <> — {resultadoImportacao.totalMescladasComManual} mesclada(s) com lançamento(s) manual(is) já existente(s)</>
          )}
          {resultadoImportacao.totalDuplicatasEntreArquivosIgnoradas > 0 && (
            <> — {resultadoImportacao.totalDuplicatasEntreArquivosIgnoradas} duplicata(s) entre arquivos ignorada(s)</>
          )}
          {resultadoImportacao.totalDuplicatasEntreArquivosSinalizadas > resultadoImportacao.totalDuplicatasEntreArquivosIgnoradas && (
            <> — {resultadoImportacao.totalDuplicatasEntreArquivosSinalizadas - resultadoImportacao.totalDuplicatasEntreArquivosIgnoradas} provável(eis) duplicata(s) entre arquivos importada(s) mesmo assim</>
          )}
        </div>
      )}

      {resultadoImportacao && resultadoImportacao.parcelamentosDetectados.length > 0 && (
        <div className="cd-msg" style={{ marginTop: -8, marginBottom: 16, fontSize: 13 }}>
          🔁 Encontrei {resultadoImportacao.parcelamentosDetectados.length} parcelamento(s):{' '}
          {resultadoImportacao.parcelamentosDetectados.map((p, i) => (
            <span key={p.parcelamentoId}>
              {i > 0 && ', '}
              <strong>{p.comerciante} {p.numeroParcela}/{p.totalParcelas}</strong>
              {p.status === 'Quitado'
                ? ' (quitado)'
                : ` (faltam ${p.parcelasRestantes} · ${fmtBRL(p.valorRestante)})`}
            </span>
          ))}
          . ✓ parcelas futuras criadas em Contas a Pagar.
        </div>
      )}

      {resultadoImportacao && resultadoImportacao.avisosParcelamento.length > 0 && (
        <div className="cd-msg" style={{ marginTop: -8, marginBottom: 16, fontSize: 13, color: 'var(--warning)' }}>
          ⚠ {resultadoImportacao.avisosParcelamento.map((aviso, i) => (
            <span key={i}>{i > 0 && ' '}{aviso}</span>
          ))}
        </div>
      )}

      {resultadoImportacao && resultadoImportacao.totalSugestoesVinculo > 0 && !sugestoesConfirmadas && (
        <div className="cd-msg" style={{ marginTop: -8, marginBottom: 16 }}>
          💡 {resultadoImportacao.totalSugestoesVinculo} conta(s) prevista(s) encontrada(s) no extrato importado.{' '}
          <button
            type="button"
            onClick={handleConfirmarTodasSugestoes}
            disabled={confirmandoTodasSugestoes}
            style={{ background: 'none', border: 'none', color: 'var(--accent)', cursor: 'pointer', padding: 0, font: 'inherit', textDecoration: 'underline', marginRight: 10 }}
          >
            {confirmandoTodasSugestoes ? 'Confirmando...' : 'Confirmar todas'}
          </button>
          <button
            type="button"
            onClick={() => navigate('/contas')}
            style={{ background: 'none', border: 'none', color: 'var(--accent)', cursor: 'pointer', padding: 0, font: 'inherit', textDecoration: 'underline' }}
          >
            Revisar
          </button>
        </div>
      )}
      {sugestoesConfirmadas && (
        <div className="cd-msg cd-msg-sucesso" style={{ marginTop: -8, marginBottom: 16 }}>
          ✅ Sugestões de vínculo confirmadas.
        </div>
      )}

      {conta.tipo === 'Investimento' && (
        <div className="stats-grid" style={{ marginBottom: 16 }}>
          <div className="cb-resumo-item">
            <span className="cb-resumo-label">Total aportado</span>
            <span className="cb-resumo-val">{fmtBRL(conta.totalAportado ?? 0)}</span>
          </div>
          <div className="cb-resumo-item">
            <span className="cb-resumo-label">Rendimento acumulado</span>
            <span className={`cb-resumo-val ${(conta.rendimentoAcumulado ?? 0) >= 0 ? 'val-green' : 'val-red'}`}>
              {fmtBRL(conta.rendimentoAcumulado ?? 0)}
            </span>
          </div>
          <div className="cb-resumo-item">
            <span className="cb-resumo-label">Saldo atual</span>
            <span className={`cb-resumo-val ${conta.saldoAtual < 0 ? 'val-red' : 'val-green'}`}>{fmtBRL(conta.saldoAtual)}</span>
          </div>
          <div className="cb-resumo-item">
            <span className="cb-resumo-label">Rentabilidade no período</span>
            <span className={`cb-resumo-val ${(conta.rentabilidadePercentual ?? 0) >= 0 ? 'val-green' : 'val-red'}`}>
              {conta.rentabilidadePercentual != null ? fmtPct(conta.rentabilidadePercentual) : '—'}
            </span>
          </div>
        </div>
      )}

      {/* Bloco 5 (Money Vision), item 3: resumo do MÊS + "última atualização" + lembrete de 30 dias. */}
      {conta.tipo === 'Investimento' && (
        <div className="contas-section">
          <h3>📅 Este mês</h3>
          <div className="stats-grid" style={{ marginBottom: 10 }}>
            <div className="cb-resumo-item">
              <span className="cb-resumo-label">Aportes</span>
              <span className="cb-resumo-val val-green">{fmtBRL(conta.aporteMesAtual ?? 0)}</span>
            </div>
            <div className="cb-resumo-item">
              <span className="cb-resumo-label">Resgates</span>
              <span className="cb-resumo-val val-red">{fmtBRL(conta.resgateMesAtual ?? 0)}</span>
            </div>
            <div className="cb-resumo-item">
              <span className="cb-resumo-label">Aporte líquido</span>
              <span className={`cb-resumo-val ${(conta.aporteLiquidoMesAtual ?? 0) >= 0 ? 'val-green' : 'val-red'}`}>
                {fmtBRL(conta.aporteLiquidoMesAtual ?? 0)}
              </span>
            </div>
            <div className="cb-resumo-item">
              <span className="cb-resumo-label">Rendimento do mês</span>
              <span className={`cb-resumo-val ${(conta.rendimentoMesAtual ?? 0) >= 0 ? 'val-green' : 'val-red'}`}>
                {fmtBRL(conta.rendimentoMesAtual ?? 0)}
              </span>
            </div>
            <div className="cb-resumo-item">
              <span className="cb-resumo-label">Rentabilidade do mês</span>
              <span className={`cb-resumo-val ${(conta.rentabilidadeMesAtualPercentual ?? 0) >= 0 ? 'val-green' : 'val-red'}`}>
                {conta.rentabilidadeMesAtualPercentual != null ? fmtPct(conta.rentabilidadeMesAtualPercentual) : '—'}
              </span>
            </div>
            <div className="cb-resumo-item">
              <span className="cb-resumo-label">Rentabilidade no ano</span>
              <span className={`cb-resumo-val ${(conta.rentabilidadeAnoAtualPercentual ?? 0) >= 0 ? 'val-green' : 'val-red'}`}>
                {conta.rentabilidadeAnoAtualPercentual != null ? fmtPct(conta.rentabilidadeAnoAtualPercentual) : '—'}
              </span>
            </div>
          </div>
          <p style={{ fontSize: 12, color: diasDesdeUltimaAtualizacao != null && diasDesdeUltimaAtualizacao > 30 ? 'var(--warning)' : 'var(--tx3)' }}>
            {conta.ultimaAtualizacaoRendimento
              ? `Última atualização: ${fmtDate(conta.ultimaAtualizacaoRendimento)}`
              : 'Ainda não foi atualizado — clique em "Atualizar saldo" acima.'}
            {diasDesdeUltimaAtualizacao != null && diasDesdeUltimaAtualizacao > 30 && ' — já passou de 30 dias, vale atualizar.'}
          </p>

          {historicoSaldo.length > 0 && (
            <div style={{ height: 180, marginTop: 12 }}>
              <ResponsiveContainer width="100%" height="100%">
                <LineChart data={historicoSaldo.map(h => ({ mes: h.mes.slice(5) + '/' + h.mes.slice(2, 4), saldo: h.saldo }))}>
                  <CartesianGrid strokeDasharray="3 3" stroke="var(--bd)" />
                  <XAxis dataKey="mes" stroke="var(--tx3)" tick={{ fontSize: 11 }} />
                  <YAxis stroke="var(--tx3)" tick={{ fontSize: 11 }} tickFormatter={v => `R$${(v / 1000).toFixed(0)}k`} />
                  <Tooltip formatter={(v) => typeof v === 'number' ? fmtBRL(v) : String(v)} contentStyle={{ background: 'var(--bg-card)', border: '1px solid var(--bd)' }} />
                  <Line type="monotone" dataKey="saldo" stroke="var(--mv-blue-light)" strokeWidth={2} dot={false} />
                </LineChart>
              </ResponsiveContainer>
            </div>
          )}
        </div>
      )}

      {conta.tipo === 'Investimento' && (
        <div className="contas-section">
          <h3>🎯 Meta(s) vinculada(s)</h3>
          {conta.metasVinculadas && conta.metasVinculadas.length > 0 ? (
            <>
              {conta.progressoCombinadoPercentual !== null && conta.progressoCombinadoPercentual !== undefined && (
                <p style={{ fontSize: 13, color: 'var(--tx3)', marginBottom: 8 }}>
                  Progresso combinado: <strong style={{ color: 'var(--tx1)' }}>{fmtPct(conta.progressoCombinadoPercentual)}</strong> do saldo desta conta em relação à soma das metas vinculadas.
                </p>
              )}
              {conta.metasVinculadas.map(m => (
                <div key={m.id} className="conta-item">
                  <div className="conta-info">
                    <div className="conta-desc">{m.sonho || `Meta ${m.ano}`}</div>
                    <div className="conta-meta">
                      Ano {m.ano} · Alvo: {fmtBRL(m.valorSonho)}
                      {m.previsaoAtingimentoMesAno && (
                        <> · Faltam {fmtBRL(Math.max(0, m.valorSonho - conta.saldoAtual))}. No seu ritmo de aporte (média dos últimos 3 meses) você chega lá em {m.previsaoAtingimentoMesAno}.</>
                      )}
                    </div>
                  </div>
                  <button className="cb-btn-inativar" onClick={() => handleDesvincular(m.id)}>Desvincular</button>
                </div>
              ))}
            </>
          ) : (
            <p style={{ color: 'var(--tx3)', fontSize: 13 }}>Nenhuma meta vinculada ainda.</p>
          )}
          <button className="btn-add-conta" style={{ marginTop: 8 }} onClick={abrirModalVincular}>＋ Vincular meta</button>
        </div>
      )}

      {msg && <div className="cd-msg">{msg}</div>}

      <div className="cd-tabs">
        <button className={`cd-tab ${aba === 'extrato' ? 'active' : ''}`} onClick={() => setAba('extrato')}>
          Extrato
        </button>
        <button className={`cd-tab ${aba === 'recebiveis' ? 'active' : ''}`} onClick={() => setAba('recebiveis')}>
          Recebíveis {recebiveis.length > 0 && `(${recebiveis.length})`}
        </button>
        <button className={`cd-tab ${aba === 'pagamentos' ? 'active' : ''}`} onClick={() => setAba('pagamentos')}>
          Pagamentos {pagamentos.length > 0 && `(${pagamentos.length})`}
        </button>
      </div>

      {aba === 'extrato' && (
        <>
          <div className="cd-filtro-periodo">
            <label>De <input type="date" value={de} onChange={e => setDe(e.target.value)} /></label>
            <label>Até <input type="date" value={ate} onChange={e => setAte(e.target.value)} /></label>
            <label className="cd-toggle-saldo-linha">
              <input type="checkbox" checked={mostrarSaldoPorLinha} onChange={e => setMostrarSaldoPorLinha(e.target.checked)} />
              Mostrar saldo por linha
            </label>
          </div>

          {/* Barra de filtros (item 6, Bloco 2) */}
          <div className="cd-filtros-extrato">
            <input
              type="text"
              className="cd-filtro-busca"
              placeholder="🔎 Buscar na descrição..."
              value={filtroTexto}
              onChange={e => setFiltroTexto(e.target.value)}
            />
            <select value={filtroTipo} onChange={e => setFiltroTipo(e.target.value as typeof filtroTipo)}>
              <option value="todos">Entradas e saídas</option>
              <option value="entrada">Só entradas</option>
              <option value="saida">Só saídas</option>
            </select>
            <select value={filtroCategoria} onChange={e => setFiltroCategoria(e.target.value)}>
              <option value="">Todas as categorias</option>
              {categoriasNomes.map(nome => <option key={nome} value={nome}>{nome}</option>)}
            </select>
            <label className="cd-filtro-check">
              <input type="checkbox" checked={filtroSoSemCategoria} onChange={e => setFiltroSoSemCategoria(e.target.checked)} />
              Só sem categoria
            </label>
            <label className="cd-filtro-check">
              <input type="checkbox" checked={filtroSoTransferenciaInvestimento} onChange={e => setFiltroSoTransferenciaInvestimento(e.target.checked)} />
              Só transferências/investimentos
            </label>
          </div>

          {/* Barra de seleção em lote (item 7, Bloco 2) */}
          {selecionados.size > 0 && (
            <div className="cd-lote-barra">
              <span>{selecionados.size} selecionado(s)</span>
              <button type="button" className="cd-lote-btn" onClick={() => setLoteCategoriaAberta(true)}>Aplicar categoria</button>
              <button type="button" className="cd-lote-btn" onClick={() => setLoteTransferenciaAberta(true)}>Marcar como transferência</button>
              <button type="button" className="cd-lote-btn cd-lote-btn-excluir" onClick={() => setLoteExcluirAberta(true)}>Excluir</button>
              <button type="button" className="cd-lote-limpar" onClick={limparSelecao}>Limpar seleção</button>
            </div>
          )}

          {loadingExtrato ? (
            <p style={{ color: 'var(--tx3)' }}>Carregando extrato...</p>
          ) : lancamentos.length === 0 ? (
            <p style={{ color: 'var(--tx3)', fontSize: 13 }}>Nenhum lançamento no período selecionado.</p>
          ) : gruposPorDia.length === 0 ? (
            <p style={{ color: 'var(--tx3)', fontSize: 13 }}>Nenhum lançamento casa com os filtros.</p>
          ) : (
            gruposPorDia.map(grupo => (
              <div key={grupo.data} className="cd-extrato-grupo-dia">
                <div className="cd-extrato-dia-cabecalho">
                  <span className="cd-extrato-dia-data">{fmtDate(grupo.data)}</span>
                  <span className="cd-extrato-dia-resumo">
                    {grupo.entradas > 0 && <span className="val-green">Entradas +{fmtBRL(grupo.entradas)}</span>}
                    {grupo.saidas > 0 && <span className="val-red">Saídas −{fmtBRL(grupo.saidas)}</span>}
                    <span>Saldo do dia {fmtBRL(grupo.saldoDoDia)}</span>
                  </span>
                </div>

                {/* Desktop/tablet (≥768px) — linhas em grid; ver .cd-extrato-cards pra <768px */}
                <div className={`cd-extrato-lista${mostrarSaldoPorLinha ? ' cd-extrato-lista--com-saldo' : ''}`}>
                  {grupo.itens.map((l, i) => {
                    const chave = `${l.data}-${l.id ?? i}`
                    const { titulo, subtitulo } = limparDescricao(l.descricao)
                    return (
                      <div key={chave} className="cd-extrato-linha">
                        <input
                          type="checkbox"
                          className="cd-checkbox-selecao"
                          disabled={!l.id}
                          checked={!!l.id && selecionados.has(chaveLancamento(l))}
                          onChange={() => toggleSelecao(l)}
                          aria-label={`Selecionar "${l.descricao}"`}
                        />
                        {renderAcoesLinha(l)}
                        <span className="cd-extrato-desc" title={l.descricao}>
                          <span className="cd-extrato-desc-titulo">{titulo}</span>
                          {subtitulo && <span className="cd-extrato-desc-subtitulo">{subtitulo}</span>}
                        </span>
                        {renderChip(l, chave)}
                        <span className={`cd-col-valor ${l.valor >= 0 ? 'val-green' : 'val-red'}`}>
                          {l.valor >= 0 ? '+' : ''}{fmtBRL(l.valor)}
                        </span>
                        {mostrarSaldoPorLinha && <span className="cd-col-valor cd-col-saldo-linha">{fmtBRL(l.saldoAcumulado)}</span>}
                      </div>
                    )
                  })}
                </div>

                {/* Mobile (<768px) — cards tocáveis, ações na bottom sheet (ver acaoMobileLancamento) */}
                <div className="cd-extrato-cards">
                  {grupo.itens.map((l, i) => {
                    const chave = `${l.data}-${l.id ?? i}`
                    const tipo = classificarChip(l)
                    const { titulo, subtitulo } = limparDescricao(l.descricao)
                    return (
                      <div key={chave} className="cd-extrato-card">
                        <input
                          type="checkbox"
                          className="cd-checkbox-selecao"
                          disabled={!l.id}
                          checked={!!l.id && selecionados.has(chaveLancamento(l))}
                          onChange={() => toggleSelecao(l)}
                          onClick={e => e.stopPropagation()}
                          aria-label={`Selecionar "${l.descricao}"`}
                        />
                        <button
                          type="button"
                          className="cd-extrato-card-corpo"
                          onClick={() => l.id && setAcaoMobileLancamento(l)}
                        >
                          <span className="cd-extrato-card-desc">
                            <span className="cd-extrato-desc-titulo">{titulo}</span>
                            {subtitulo && <span className="cd-extrato-desc-subtitulo">{subtitulo}</span>}
                            <span className={`cd-chip cd-chip-${tipo}`}>
                              {rotuloChip(l, tipo)}
                            </span>
                          </span>
                          <span className={`cd-col-valor ${l.valor >= 0 ? 'val-green' : 'val-red'}`}>
                            {l.valor >= 0 ? '+' : ''}{fmtBRL(l.valor)}
                          </span>
                        </button>
                      </div>
                    )
                  })}
                </div>
              </div>
            ))
          )}
        </>
      )}

      {(aba === 'recebiveis' || aba === 'pagamentos') && !loadingPendencias && (
        <>
          {atrasadosPendencias.length > 0 && (
            <button type="button" className="contas-atrasadas-aviso" onClick={() => setModoPendencias('atrasadas')}>
              ⚠️ {atrasadosPendencias.length} pendência(s) atrasada(s) · {fmtBRL(atrasadosPendencias.reduce((s, p) => s + p.valor, 0))}
            </button>
          )}
          <div className="contas-mes-seletor">
            <button type="button" className="contas-mes-seta" onClick={() => { setMesPendenciasSelecionado(m => deslocarMes(m, -1)); setModoPendencias('mes') }}>‹</button>
            <span className="contas-mes-label">{labelMes(mesPendenciasSelecionado)}</span>
            <button type="button" className="contas-mes-seta" onClick={() => { setMesPendenciasSelecionado(m => deslocarMes(m, 1)); setModoPendencias('mes') }}>›</button>
            <div className="contas-mes-atalhos">
              <button type="button" className={modoPendencias === 'mes' && mesPendenciasSelecionado === mesAtualISO() ? 'ativo' : ''}
                onClick={() => { setMesPendenciasSelecionado(mesAtualISO()); setModoPendencias('mes') }}>Este mês</button>
              <button type="button" className={modoPendencias === 'mes' && mesPendenciasSelecionado === deslocarMes(mesAtualISO(), 1) ? 'ativo' : ''}
                onClick={() => { setMesPendenciasSelecionado(deslocarMes(mesAtualISO(), 1)); setModoPendencias('mes') }}>Próximo mês</button>
              <button type="button" className={modoPendencias === 'atrasadas' ? 'ativo' : ''} onClick={() => setModoPendencias('atrasadas')}>Atrasadas</button>
              <button type="button" className={modoPendencias === 'tudo' ? 'ativo' : ''} onClick={() => setModoPendencias('tudo')}>Tudo</button>
            </div>
          </div>
        </>
      )}

      {aba === 'recebiveis' && (
        loadingPendencias ? <p style={{ color: 'var(--tx3)' }}>Carregando...</p> :
        recebiveisVisiveis.length === 0 ? <p style={{ color: 'var(--tx3)', fontSize: 13 }}>Nenhum recebível pendente nesse período.</p> :
        <div className="contas-section">{recebiveisVisiveis.map(r => renderPendencia(r, 'receber'))}</div>
      )}

      {aba === 'pagamentos' && (
        loadingPendencias ? <p style={{ color: 'var(--tx3)' }}>Carregando...</p> :
        pagamentosVisiveis.length === 0 ? <p style={{ color: 'var(--tx3)', fontSize: 13 }}>Nenhum pagamento pendente nesse período.</p> :
        <div className="contas-section">{pagamentosVisiveis.map(p => renderPendencia(p, 'pagar'))}</div>
      )}

      <Modal
        open={modalRendimento}
        title="📈 Atualizar saldo"
        onClose={() => setModalRendimento(false)}
        footer={
          <>
            <button className="btn-cancel" onClick={() => setModalRendimento(false)}>Cancelar</button>
            <button
              className="btn-confirm"
              onClick={handleSalvarRendimento}
              disabled={salvandoRendimento || (modoSaldo ? !diferencaSaldo : rendValor <= 0)}
            >
              {salvandoRendimento ? 'Salvando...' : 'Confirmar'}
            </button>
          </>
        }
      >
        <button
          type="button"
          onClick={() => setModoSaldo(!modoSaldo)}
          style={{ background: 'none', border: 'none', color: 'var(--accent)', cursor: 'pointer', padding: 0, font: 'inherit', textDecoration: 'underline', marginBottom: 12, display: 'block' }}
        >
          {modoSaldo ? 'Ou lançar o valor do rendimento direto' : 'Ou digitar o saldo atual no banco'}
        </button>

        {modoSaldo ? (
          <div className="inp-group">
            <label>Saldo atual no app do banco</label>
            <div className="val-input-wrap">
              <span className="val-prefix">R$</span>
              <input
                type="text" inputMode="decimal" placeholder="0,00"
                value={saldoBancoDisplay}
                onChange={e => {
                  const raw = e.target.value.replace(/[^\d,]/g, '')
                  setSaldoBancoDisplay(raw)
                  setSaldoBanco(parseFloat(raw.replace(',', '.')) || 0)
                }}
              />
            </div>
            {saldoBancoDisplay && diferencaSaldo != null && (
              <p style={{ fontSize: 12, marginTop: 6, color: diferencaSaldo >= 0 ? 'var(--success)' : 'var(--danger)' }}>
                {diferencaSaldo >= 0
                  ? `Rendimento de ${fmtBRL(diferencaSaldo)} a lançar.`
                  : `Ajuste negativo de ${fmtBRL(-diferencaSaldo)} (IR/IOF/oscilação).`}
              </p>
            )}
          </div>
        ) : (
          <>
            <div style={{ marginBottom: 12, display: 'flex', gap: 8 }}>
              <button
                type="button"
                onClick={() => setRendPositivo(true)}
                style={{
                  padding: '6px 14px', borderRadius: 8, cursor: 'pointer', fontSize: 13,
                  border: rendPositivo ? '1px solid var(--accent)' : '1px solid var(--bd)',
                  background: rendPositivo ? 'var(--accent)' : 'var(--bg-card)',
                  color: rendPositivo ? '#fff' : 'var(--tx1)',
                }}>
                Ganho
              </button>
              <button
                type="button"
                onClick={() => setRendPositivo(false)}
                style={{
                  padding: '6px 14px', borderRadius: 8, cursor: 'pointer', fontSize: 13,
                  border: !rendPositivo ? '1px solid var(--accent)' : '1px solid var(--bd)',
                  background: !rendPositivo ? 'var(--accent)' : 'var(--bg-card)',
                  color: !rendPositivo ? '#fff' : 'var(--tx1)',
                }}>
                Perda
              </button>
            </div>
            <div className="inp-group">
              <label>Valor</label>
              <div className="val-input-wrap">
                <span className="val-prefix">R$</span>
                <input
                  type="text" inputMode="decimal" placeholder="0,00"
                  value={rendValorDisplay}
                  onChange={e => {
                    const raw = e.target.value.replace(/[^\d,]/g, '')
                    setRendValorDisplay(raw)
                    setRendValor(parseFloat(raw.replace(',', '.')) || 0)
                  }}
                />
              </div>
            </div>
          </>
        )}
        <div className="inp-group">
          <label>Data</label>
          <input type="date" value={rendData} max={todayISO()} onChange={e => setRendData(e.target.value)} />
        </div>
        <div className="inp-group">
          <label>Descrição (opcional)</label>
          <input value={rendDescricao} onChange={e => setRendDescricao(e.target.value)} placeholder="Ex: Rendimento do mês" />
        </div>
      </Modal>

      <Modal
        open={modalConferirSaldo}
        title="⚖️ Conferir saldo"
        onClose={() => setModalConferirSaldo(false)}
        footer={
          <>
            <button className="btn-cancel" onClick={() => setModalConferirSaldo(false)}>Cancelar</button>
            <button
              className="btn-confirm"
              onClick={handleConferirSaldo}
              disabled={salvandoConferirSaldo || !conferirSaldoDisplay || conferirSaldoReal === conta.saldoAtual}
            >
              {salvandoConferirSaldo ? 'Salvando...' : 'Confirmar'}
            </button>
          </>
        }
      >
        <p style={{ fontSize: 13, color: 'var(--tx3)', marginBottom: 12 }}>
          Seu saldo no app é {fmtBRL(conta.saldoAtual)}. Qual o saldo no app do banco nesta data?
        </p>
        <div className="inp-group">
          <label>Saldo no banco</label>
          <div className="val-input-wrap">
            <span className="val-prefix">R$</span>
            <input
              type="text" inputMode="decimal" placeholder="0,00"
              value={conferirSaldoDisplay}
              onChange={e => {
                const raw = e.target.value.replace(/[^\d,]/g, '')
                setConferirSaldoDisplay(raw)
                setConferirSaldoReal(parseFloat(raw.replace(',', '.')) || 0)
              }}
            />
          </div>
          {conferirSaldoDisplay && (
            (() => {
              const diferenca = conferirSaldoReal - conta.saldoAtual
              if (diferenca === 0) return <p style={{ fontSize: 12, marginTop: 6, color: 'var(--tx3)' }}>Sem diferença — nada a ajustar.</p>
              return (
                <p style={{ fontSize: 12, marginTop: 6, color: diferenca >= 0 ? 'var(--success)' : 'var(--danger)' }}>
                  Cria um ajuste de {diferenca >= 0 ? '+' : '−'}{fmtBRL(Math.abs(diferenca))} em "Ajuste de saldo" — nunca entra no DRE.
                </p>
              )
            })()
          )}
        </div>
        <div className="inp-group">
          <label>Data</label>
          <input type="date" value={conferirData} max={todayISO()} onChange={e => setConferirData(e.target.value)} />
        </div>
      </Modal>

      <Modal
        open={modalDuplicatas}
        title="🔍 Revisar duplicatas"
        onClose={() => setModalDuplicatas(false)}
      >
        {carregandoDuplicatas ? (
          <p style={{ fontSize: 13, color: 'var(--tx3)' }}>Procurando prováveis duplicatas...</p>
        ) : duplicatas.length === 0 ? (
          <p style={{ fontSize: 13, color: 'var(--tx3)' }}>Nenhuma provável duplicata encontrada nesta conta.</p>
        ) : (
          duplicatas.map(dup => (
            <div key={`${dup.lancamentoAId}-${dup.lancamentoBId}`} style={{ border: '1px solid var(--bd)', borderRadius: 8, padding: 10, marginBottom: 10 }}>
              <div style={{ fontSize: 11, color: 'var(--tx3)', marginBottom: 6 }}>{dup.tipo} · {dup.score}% parecido</div>
              <div style={{ display: 'flex', gap: 8, alignItems: 'stretch' }}>
                <div style={{ flex: 1, fontSize: 13 }}>
                  <div>{dup.descricaoA}</div>
                  <div style={{ color: 'var(--tx3)' }}>{fmtBRL(dup.valorA)} · {fmtDate(dup.dataA)}</div>
                  <button className="btn-cancel" style={{ marginTop: 6, width: '100%' }}
                    disabled={excluindoDuplicataId !== null}
                    onClick={() => excluirLadoDaDuplicata(dup, 'A')}>
                    {excluindoDuplicataId === dup.lancamentoAId ? 'Excluindo...' : 'Excluir este'}
                  </button>
                </div>
                <div style={{ flex: 1, fontSize: 13 }}>
                  <div>{dup.descricaoB}</div>
                  <div style={{ color: 'var(--tx3)' }}>{fmtBRL(dup.valorB)} · {fmtDate(dup.dataB)}</div>
                  <button className="btn-cancel" style={{ marginTop: 6, width: '100%' }}
                    disabled={excluindoDuplicataId !== null}
                    onClick={() => excluirLadoDaDuplicata(dup, 'B')}>
                    {excluindoDuplicataId === dup.lancamentoBId ? 'Excluindo...' : 'Excluir este'}
                  </button>
                </div>
              </div>
              {/* Item 3.2: não são duplicatas de verdade — grava a decisão pra não aparecer de novo. */}
              <button className="cb-btn-editar" style={{ marginTop: 8, width: '100%' }}
                disabled={excluindoDuplicataId !== null}
                onClick={() => manterOsDoisDaDuplicata(dup)}>
                {excluindoDuplicataId === dup.lancamentoAId ? 'Salvando...' : 'Manter os dois'}
              </button>
            </div>
          ))
        )}
      </Modal>

      <Modal
        open={modalVincular}
        title="🎯 Vincular meta"
        onClose={() => setModalVincular(false)}
      >
        <div className="inp-group">
          <label>Meta existente</label>
          <select value={metaSelecionadaId} onChange={e => setMetaSelecionadaId(e.target.value)}>
            <option value="">Selecione...</option>
            {metasDisponiveis.filter(m => m.contaInvestimentoId !== conta?.id).map(m => (
              <option key={m.id} value={m.id}>{m.sonho || `Meta ${m.ano}`} — {fmtBRL(m.valorSonho)}</option>
            ))}
          </select>
          <button className="btn-add-conta" style={{ marginTop: 8, width: '100%' }} onClick={handleVincularExistente} disabled={!metaSelecionadaId || salvandoVinculo}>
            {salvandoVinculo ? 'Vinculando...' : 'Vincular esta meta'}
          </button>
        </div>

        <div style={{ borderTop: '1px solid var(--bd)', margin: '16px 0', paddingTop: 16 }}>
          <h4 style={{ marginBottom: 8 }}>Ou criar uma meta nova</h4>
          <div className="inp-group">
            <label>Ano</label>
            <input type="number" value={novoAnoMeta} onChange={e => setNovoAnoMeta(+e.target.value)} />
          </div>
          <div className="inp-group">
            <label>Valor alvo</label>
            <div className="val-input-wrap">
              <span className="val-prefix">R$</span>
              <input type="text" inputMode="decimal" placeholder="0,00" value={novoValorSonho}
                onChange={e => setNovoValorSonho(e.target.value.replace(/[^\d,]/g, ''))} />
            </div>
          </div>
          <button className="btn-add-conta" style={{ width: '100%' }} onClick={handleCriarEVincular} disabled={!novoValorSonho || salvandoVinculo}>
            {salvandoVinculo ? 'Criando...' : '＋ Criar e vincular'}
          </button>
        </div>
      </Modal>

      <Modal
        open={modalImportar}
        title="⬆️ Importar extrato"
        onClose={() => setModalImportar(false)}
        footer={
          <>
            <button className="btn-cancel" onClick={() => setModalImportar(false)}>Cancelar</button>
            <button
              className="btn-confirm"
              onClick={handleConfirmarImportacao}
              disabled={
                importando || previewCarregando || !resumoImportacao || resumoImportacao.totalNovas === 0
                || (conta?.tipo === 'CartaoCredito' && (!bancoFatura.trim() || !dataVencimentoFatura))
              }
            >
              {importando ? 'Importando...' : `Importar ${resumoImportacao?.totalNovas ?? 0} transação(ões)`}
            </button>
          </>
        }
      >
        {conta?.tipo === 'CartaoCredito' && (
          <>
            <p style={{ fontSize: 12, color: 'var(--tx3)', marginBottom: 10 }}>
              Cada importação é uma fatura — todas as compras deste arquivo entram juntas, numa fatura só.
            </p>
            <div className="inp-group">
              <label>Cartão / banco</label>
              <input
                type="text" list="bancos-fatura" value={bancoFatura}
                onChange={e => setBancoFatura(e.target.value)} placeholder="Ex.: Nubank"
              />
              <datalist id="bancos-fatura">
                {bancosDisponiveis.map(b => <option key={b} value={b} />)}
              </datalist>
            </div>
            <div className="inp-group">
              <label>Vencimento</label>
              <input type="date" value={dataVencimentoFatura} onChange={e => handleAlterarVencimentoFatura(e.target.value)} />
            </div>
            <div className="inp-group">
              <label>Fechamento (opcional)</label>
              <input type="date" value={dataFechamentoFatura} onChange={e => setDataFechamentoFatura(e.target.value)} />
            </div>
            <div className="inp-group">
              <label>Referência (mês/ano)</label>
              <input
                type="text" value={competenciaFatura} placeholder="yyyy-MM"
                onChange={e => { setCompetenciaFatura(e.target.value); setCompetenciaEditadaManualmente(true) }}
              />
            </div>
          </>
        )}

        <div className="inp-group">
          <label>Importar do dia</label>
          <input type="date" value={dataInicioImport} onChange={e => handleAlterarIntervaloImportacao(e.target.value, dataFimImport)} />
        </div>
        <div className="inp-group">
          <label>Até o dia</label>
          <input type="date" value={dataFimImport} onChange={e => handleAlterarIntervaloImportacao(dataInicioImport, e.target.value)} />
        </div>

        {previewCarregando ? (
          <p style={{ fontSize: 13, color: 'var(--tx3)', margin: '12px 0' }}>Calculando resumo...</p>
        ) : resumoImportacao && (
          <div style={{ fontSize: 13, color: 'var(--tx3)', margin: '12px 0' }}>
            <p>
              <strong style={{ color: 'var(--tx1)' }}>{resumoImportacao.totalEncontradas}</strong> transação(ões) encontrada(s) no intervalo selecionado
              {resumoImportacao.totalJaImportadas > 0 && (
                <> — <strong style={{ color: 'var(--tx1)' }}>{resumoImportacao.totalJaImportadas}</strong> já importada(s) antes (ignoradas automaticamente)</>
              )}.
            </p>
            <p>
              <strong style={{ color: '#34c759' }}>{resumoImportacao.totalNovas}</strong> serão importadas agora
              {resumoImportacao.totalNovas > 0 && conta?.tipo === 'CartaoCredito' ? (
                <>
                  {' · '}
                  <span className="val-red">Compras: -{fmtBRL(resumoImportacao.totalSaidas)} ({resumoImportacao.quantidadeSaidas})</span>
                  {' · '}
                  <span className="val-green">Estornos: {fmtBRL(resumoImportacao.totalEntradas)} ({resumoImportacao.quantidadeEntradas})</span>
                </>
              ) : resumoImportacao.totalNovas > 0 && (
                <> · <span className="val-green">+{fmtBRL(resumoImportacao.totalEntradas)}</span> em entradas · <span className="val-red">-{fmtBRL(resumoImportacao.totalSaidas)}</span> em saídas</>
              )}.
            </p>
            {resumoImportacao.totalNovas > 0 && (
              <p style={{ fontSize: 12 }}>Entram como <strong>pendentes de categorização</strong> — categorize depois na tela de categorização.</p>
            )}
            {conta?.tipo === 'CartaoCredito' && (
              <div style={{ marginTop: 8 }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 12, cursor: 'pointer' }}>
                  <input
                    type="checkbox"
                    checked={inverterSinaisManualmente ?? resumoImportacao.sinalInvertidoParaCartao}
                    onChange={e => handleAlterarInverterSinais(e.target.checked)}
                  />
                  Inverter sinais (compra entrou como estorno, ou vice-versa?)
                </label>
                {inverterSinaisManualmente !== undefined && (
                  <button type="button" className="btn-cancel" style={{ fontSize: 11, padding: '2px 8px', marginTop: 4 }}
                    onClick={() => handleAlterarInverterSinais(undefined)}>
                    Voltar pra detecção automática
                  </button>
                )}
              </div>
            )}
          </div>
        )}

        {!previewCarregando && resumoImportacao && resumoImportacao.duplicatasManuais.length > 0 && (
          <div style={{ margin: '12px 0', border: '1px solid var(--bd)', borderRadius: 8, padding: 10 }}>
            <p style={{ fontSize: 13, fontWeight: 600, marginBottom: 6 }}>
              💡 {resumoImportacao.duplicatasManuais.length} já lançada(s) manualmente — o que fazer?
            </p>
            {resumoImportacao.duplicatasManuais.map(d => {
              const acao = resolucoesDuplicatas[d.transacaoIndice] ?? 'Mesclar'
              return (
                <div key={d.transacaoIndice} style={{ fontSize: 12, marginBottom: 8, paddingBottom: 8, borderBottom: '1px solid var(--bd)' }}>
                  <div style={{ color: 'var(--tx3)', marginBottom: 4 }}>
                    Banco: <strong style={{ color: 'var(--tx1)' }}>{d.descricaoBanco}</strong> — {fmtBRL(d.valor)} em {fmtDate(d.dataBanco)}
                    <br />Já lançado manualmente: <strong style={{ color: 'var(--tx1)' }}>{d.descricaoManual}</strong> em {fmtDate(d.dataManual)}
                  </div>
                  <label style={{ marginRight: 12 }}>
                    <input type="radio" name={`dup-${d.transacaoIndice}`} checked={acao === 'Mesclar'}
                      onChange={() => setResolucoesDuplicatas(prev => ({ ...prev, [d.transacaoIndice]: 'Mesclar' }))} />
                    {' '}Mesclar (recomendado)
                  </label>
                  <label>
                    <input type="radio" name={`dup-${d.transacaoIndice}`} checked={acao === 'ImportarComoNovo'}
                      onChange={() => setResolucoesDuplicatas(prev => ({ ...prev, [d.transacaoIndice]: 'ImportarComoNovo' }))} />
                    {' '}Importar como novo
                  </label>
                </div>
              )
            })}
          </div>
        )}

        {/* Item 3.1: por padrão NÃO importa (desmarcado) — o usuário decide por linha se quer
            importar mesmo assim (ex.: era só parecido, não é de fato a mesma transação). */}
        {!previewCarregando && resumoImportacao && resumoImportacao.duplicatasEntreArquivos.length > 0 && (
          <div style={{ margin: '12px 0', border: '1px solid var(--bd)', borderRadius: 8, padding: 10 }}>
            <p style={{ fontSize: 13, fontWeight: 600, marginBottom: 6 }}>
              ⚠️ {resumoImportacao.duplicatasEntreArquivos.length} provável(eis) duplicata(s) entre arquivos — não serão importadas, a menos que você marque abaixo.
            </p>
            {resumoImportacao.duplicatasEntreArquivos.map(d => {
              const importar = resolucoesDuplicatas[d.transacaoIndice] === 'Importar'
              return (
                <div key={d.transacaoIndice} style={{ fontSize: 12, marginBottom: 8, paddingBottom: 8, borderBottom: '1px solid var(--bd)' }}>
                  <div style={{ color: 'var(--tx3)', marginBottom: 4 }}>
                    Banco: <strong style={{ color: 'var(--tx1)' }}>{d.descricaoBanco}</strong> — {fmtBRL(d.valor)} em {fmtDate(d.dataBanco)}
                    <br />Já importada antes: <strong style={{ color: 'var(--tx1)' }}>{d.descricaoJaImportada}</strong> em {fmtDate(d.dataJaImportada)}
                  </div>
                  <label>
                    <input type="checkbox" checked={importar}
                      onChange={e => setResolucoesDuplicatas(prev => ({ ...prev, [d.transacaoIndice]: e.target.checked ? 'Importar' : 'Ignorar' }))} />
                    {' '}Importar mesmo assim
                  </label>
                </div>
              )
            })}
          </div>
        )}
      </Modal>

      <Modal
        open={!!lancamentoParaTransferencia}
        title="🔁 Classificar como Transferência"
        onClose={() => setLancamentoParaTransferencia(null)}
        footer={
          candidatoContrapartida ? (
            <>
              <button className="btn-cancel" onClick={() => setLancamentoParaTransferencia(null)}>Cancelar</button>
              <button className="btn-cancel" onClick={() => confirmarTransferencia(false)} disabled={convertendo}>Criar novo mesmo assim</button>
              <button className="btn-confirm" onClick={() => confirmarTransferencia(true)} disabled={convertendo}>
                {convertendo ? 'Vinculando...' : 'Vincular a esse lançamento'}
              </button>
            </>
          ) : (
            <>
              <button className="btn-cancel" onClick={() => setLancamentoParaTransferencia(null)}>Cancelar</button>
              <button className="btn-confirm" onClick={() => confirmarTransferencia(false)} disabled={convertendo || !contaContrapartidaId}>
                {convertendo ? 'Convertendo...' : 'Confirmar'}
              </button>
            </>
          )
        }
      >
        {lancamentoParaTransferencia && (
          <>
            <p style={{ fontSize: 13, color: 'var(--tx3)', marginBottom: 12 }}>
              "{lancamentoParaTransferencia.descricao}" · {fmtBRL(Math.abs(lancamentoParaTransferencia.valor))} · {fmtDate(lancamentoParaTransferencia.data)}
              <br />Não é {lancamentoParaTransferencia.valor >= 0 ? 'receita' : 'despesa'}: o dinheiro {lancamentoParaTransferencia.valor >= 0 ? 'veio de' : 'foi para'} outra conta. Qual?
            </p>
            <div className="inp-group">
              <label>Conta {lancamentoParaTransferencia.valor >= 0 ? 'de origem' : 'de destino'}</label>
              <select value={contaContrapartidaId} onChange={e => setContaContrapartidaId(e.target.value)}>
                <option value="">Selecione...</option>
                {contasBancarias.filter(c => c.ativa && c.id !== contaId).map(c => (
                  <option key={c.id} value={c.id}>{c.nome}</option>
                ))}
              </select>
            </div>
            {buscandoCandidato && <p style={{ fontSize: 12, color: 'var(--tx3)', marginTop: 8 }}>Procurando lançamento correspondente...</p>}
            {candidatoContrapartida && (
              <p style={{ fontSize: 13, color: 'var(--warning)', marginTop: 8 }}>
                Já existe um lançamento parecido nessa conta: "{candidatoContrapartida.descricao}" de {fmtBRL(Math.abs(candidatoContrapartida.valor))} em {fmtDate(candidatoContrapartida.data)}.
                Vincular a ele evita duplicar a transferência.
              </p>
            )}
          </>
        )}
      </Modal>

      <Modal
        open={!!lancamentoParaEditar}
        title="✏️ Editar categoria"
        onClose={() => setLancamentoParaEditar(null)}
        footer={
          <>
            <button className="btn-cancel" onClick={() => setLancamentoParaEditar(null)}>Cancelar</button>
            <button className="btn-confirm" onClick={confirmarEdicaoCategoria} disabled={!categoriaEdicao || salvandoEdicao}>
              {salvandoEdicao ? 'Salvando...' : 'Salvar'}
            </button>
          </>
        }
      >
        {lancamentoParaEditar && (
          <>
            <p style={{ fontSize: 13, color: 'var(--tx3)', marginBottom: 12 }}>
              "{lancamentoParaEditar.descricao}" · {fmtBRL(Math.abs(lancamentoParaEditar.valor))} · {fmtDate(lancamentoParaEditar.data)}
              {lancamentoParaEditar.regraCategorizacaoId && (
                <><br />⚙️ Categoria atual veio de uma regra automática.</>
              )}
            </p>
            <div className="inp-group">
              <label>Categoria</label>
              <CategoriaCombobox
                clienteId={clienteId}
                categorias={lancamentoParaEditar.valor >= 0 ? categorias.entradas : categorias.saidas}
                value={categoriaEdicao}
                onChange={setCategoriaEdicao}
                onCategoriaCriada={handleCategoriaCriada}
                blocoPadraoNovaCategoria={lancamentoParaEditar.valor >= 0 ? 'RECEITAS OPERACIONAIS' : 'DESPESAS OPERACIONAIS'}
              />
            </div>
          </>
        )}
      </Modal>

      {/* Item 1, Bloco 1: confirmação de exclusão deixou de ser um confirm() nativo — agora é
          modal com botão vermelho (--danger), igual o padrão do resto do app. */}
      <Modal
        open={!!excluirConfirmacao}
        title="🗑 Excluir lançamento"
        onClose={() => setExcluirConfirmacao(null)}
        footer={
          <>
            <button className="btn-cancel" onClick={() => setExcluirConfirmacao(null)}>Cancelar</button>
            <button className="btn-danger" onClick={confirmarExclusao} disabled={excluindoLancamento}>
              {excluindoLancamento ? 'Excluindo...' : 'Excluir'}
            </button>
          </>
        }
      >
        {excluirConfirmacao && (
          <p style={{ fontSize: 13, color: 'var(--tx3)' }}>
            Excluir "{excluirConfirmacao.descricao}"? Essa ação não pode ser desfeita.
            {excluirConfirmacao.categoria === 'Transferência'
              ? ' Esse lançamento é uma ponta de Transferência — a contrapartida na outra conta também será excluída.'
              : ' Se ele foi usado para dar baixa em alguma conta a pagar/receber, o título volta a ficar em aberto.'}
          </p>
        )}
      </Modal>

      {/* Item 7, Bloco 2: aplicar categoria a todos os lançamentos selecionados de uma vez. */}
      <Modal
        open={loteCategoriaAberta}
        title="🏷️ Aplicar categoria em lote"
        onClose={() => setLoteCategoriaAberta(false)}
        footer={
          <>
            <button className="btn-cancel" onClick={() => setLoteCategoriaAberta(false)}>Cancelar</button>
            <button className="btn-confirm" onClick={confirmarLoteCategoria} disabled={!loteCategoriaValor || processandoLote}>
              {processandoLote ? 'Aplicando...' : 'Aplicar'}
            </button>
          </>
        }
      >
        <p style={{ fontSize: 13, color: 'var(--tx3)', marginBottom: 12 }}>
          {itensSelecionados.length} lançamento(s) selecionado(s) vão receber a mesma categoria.
        </p>
        <div className="inp-group">
          <label>Categoria</label>
          <CategoriaCombobox
            clienteId={clienteId}
            categorias={[...categorias.entradas, ...categorias.saidas]}
            value={loteCategoriaValor}
            onChange={setLoteCategoriaValor}
            onCategoriaCriada={handleCategoriaCriada}
            blocoPadraoNovaCategoria="DESPESAS OPERACIONAIS"
          />
        </div>
      </Modal>

      {/* Item 7, Bloco 2: marcar vários lançamentos como transferência pra uma mesma conta
          contrapartida — cada um cria sua própria perna automática na conta destino. */}
      <Modal
        open={loteTransferenciaAberta}
        title="⇄ Marcar lote como transferência"
        onClose={() => setLoteTransferenciaAberta(false)}
        footer={
          <>
            <button className="btn-cancel" onClick={() => setLoteTransferenciaAberta(false)}>Cancelar</button>
            <button className="btn-confirm" onClick={confirmarLoteTransferencia} disabled={!loteContaContrapartidaId || processandoLote}>
              {processandoLote ? 'Convertendo...' : 'Confirmar'}
            </button>
          </>
        }
      >
        <p style={{ fontSize: 13, color: 'var(--tx3)', marginBottom: 12 }}>
          {itensSelecionados.length} lançamento(s) selecionado(s) viram transferência — cada um cria
          automaticamente a contrapartida na conta escolhida.
        </p>
        <div className="inp-group">
          <label>Conta contrapartida</label>
          <select value={loteContaContrapartidaId} onChange={e => setLoteContaContrapartidaId(e.target.value)}>
            <option value="">Selecione...</option>
            {contasBancarias.filter(c => c.ativa && c.id !== contaId).map(c => (
              <option key={c.id} value={c.id}>{c.nome}</option>
            ))}
          </select>
        </div>
      </Modal>

      {/* Item 7, Bloco 2: exclusão em lote — mesmo padrão de botão vermelho da exclusão individual. */}
      <Modal
        open={loteExcluirAberta}
        title="🗑 Excluir lançamentos selecionados"
        onClose={() => setLoteExcluirAberta(false)}
        footer={
          <>
            <button className="btn-cancel" onClick={() => setLoteExcluirAberta(false)}>Cancelar</button>
            <button className="btn-danger" onClick={confirmarLoteExcluir} disabled={processandoLote}>
              {processandoLote ? 'Excluindo...' : 'Excluir'}
            </button>
          </>
        }
      >
        {(() => {
          const total = itensSelecionados.reduce((s, l) => s + l.valor, 0)
          const transferencias = itensSelecionados.filter(l => l.categoria === 'Transferência').length
          return (
            <>
              <p style={{ fontSize: 13, color: 'var(--tx3)' }}>
                Excluir {itensSelecionados.length} lançamento(s)? Total: {total >= 0 ? '+' : '−'}{fmtBRL(Math.abs(total))}. Essa ação não pode ser desfeita.
              </p>
              <ul style={{ fontSize: 12, color: 'var(--tx3)', margin: '8px 0', paddingLeft: 18 }}>
                {itensSelecionados.slice(0, 5).map((l, i) => <li key={i}>{l.descricao}</li>)}
              </ul>
              {itensSelecionados.length > 5 && (
                <p style={{ fontSize: 12, color: 'var(--tx3)' }}>+ {itensSelecionados.length - 5} outros</p>
              )}
              {transferencias > 0 && (
                <p style={{ fontSize: 12, color: 'var(--warning)' }}>
                  ⚠ {transferencias} transferência(s) — a contrapartida na outra conta também será excluída.
                </p>
              )}
            </>
          )
        })()}
      </Modal>

      {/* Item 8, Bloco 2: depois de categorizar manualmente, oferece aplicar a todos os parecidos
          e criar uma regra pras próximas importações — nunca pergunta de novo se o item já veio
          de uma regra (esse caso já é tratado como "atualizar a regra existente"). */}
      <Modal
        open={!!ofertaRegra}
        title="⚙️ Aplicar a todos e criar regra?"
        onClose={() => setOfertaRegra(null)}
        footer={
          <>
            <button className="btn-cancel" onClick={() => setOfertaRegra(null)}>Não, só esta</button>
            <button className="btn-confirm" onClick={confirmarOfertaRegra} disabled={criandoOfertaRegra}>
              {criandoOfertaRegra ? 'Aplicando...' : 'Aplicar e criar regra'}
            </button>
          </>
        }
      >
        {ofertaRegra && (
          <p style={{ fontSize: 13, color: 'var(--tx3)' }}>
            Encontrei {ofertaRegra.contagem} outro(s) lançamento(s) parecido(s) com "{ofertaRegra.lancamento.descricao}".
            Aplicar "{ofertaRegra.categoria}" a todos eles e criar uma regra pra categorizar
            automaticamente nas próximas importações desta conta?
          </p>
        )}
      </Modal>

      {/* Item 9, Bloco 1: no mobile (<768px), tocar num card abre essa bottom sheet com as mesmas
          3 ações da coluna de ícones do desktop — nunca um menu separado com lógica própria. */}
      {acaoMobileLancamento && (
        <div className="cd-sheet-overlay" onClick={() => setAcaoMobileLancamento(null)}>
          <div className="cd-sheet" onClick={e => e.stopPropagation()}>
            <div className="cd-sheet-desc">{limparDescricao(acaoMobileLancamento.descricao).titulo}</div>
            {acaoMobileLancamento.tipoCusto === 'AjusteSaldo' ? (
              <div className="cd-sheet-aviso">Ajuste de saldo — não é reclassificável, só pode ser excluído.</div>
            ) : (
              <>
                <button
                  type="button"
                  className="cd-sheet-acao"
                  onClick={() => { abrirModalEditarCategoria(acaoMobileLancamento); setAcaoMobileLancamento(null) }}
                >
                  ✏️ Editar categoria
                </button>
                {acaoMobileLancamento.categoria === 'Transferência' ? (
                  <button
                    type="button"
                    className="cd-sheet-acao"
                    onClick={() => {
                      if (acaoMobileLancamento.transferenciaId) handleDesfazerTransferencia(acaoMobileLancamento.transferenciaId)
                      setAcaoMobileLancamento(null)
                    }}
                  >
                    ↩ Desfazer transferência
                  </button>
                ) : (
                  <button
                    type="button"
                    className="cd-sheet-acao"
                    onClick={() => { abrirModalTransferencia(acaoMobileLancamento); setAcaoMobileLancamento(null) }}
                  >
                    ⇄ Marcar como transferência
                  </button>
                )}
              </>
            )}
            <button
              type="button"
              className="cd-sheet-acao cd-sheet-acao-excluir"
              onClick={() => { abrirConfirmarExclusao(acaoMobileLancamento); setAcaoMobileLancamento(null) }}
            >
              🗑 Excluir
            </button>
            <button type="button" className="cd-sheet-cancelar" onClick={() => setAcaoMobileLancamento(null)}>Cancelar</button>
          </div>
        </div>
      )}
    </>
  )
}
