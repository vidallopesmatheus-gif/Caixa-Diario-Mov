import { useState, useEffect, useCallback, useRef } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'
import {
  listarContasBancarias,
  obterExtratoConta,
  obterPendenciasConta,
  registrarRendimento,
  vincularMeta,
  desvincularMeta,
  listarDuplicatas,
  manterDuplicata,
} from '../../api/contasBancarias'
import { previewExtrato, importarExtrato, categorizarPendentes, excluirLancamento } from '../../api/importacao'
import type { DuplicataProvavel } from '../../api/contasBancarias'
import type { ResolucaoDuplicata } from '../../api/importacao'
import { converterLancamentoEmTransferencia, desfazerClassificacaoTransferencia } from '../../api/transferencias'
import { listarSugestoesVinculo } from '../../api/conciliacao'
import { atualizarContaProvisionada } from '../../api/contasProvisionadas'
import { listarRegras, atualizarRegra } from '../../api/regras'
import { buscarCandidatoContrapartida } from '../../utils/candidatoTransferencia'
import { listarMetas, salvarMeta } from '../../api/metas'
import { listarCategorias } from '../../api/categorias'
import { fmtBRL, fmtPct, fmtDate, todayISO, addDays } from '../../utils/format'
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
  // Fase 1.7: decisão por linha pra duplicata manual (índice ausente = Mesclar, padrão).
  // Fase 3.1: mesmo mapa pra duplicata entre arquivos (índice ausente = Ignorar, padrão).
  const [resolucoesDuplicatas, setResolucoesDuplicatas] = useState<Record<number, 'Mesclar' | 'ImportarComoNovo' | 'Ignorar' | 'Importar'>>({})
  const [confirmandoTodasSugestoes, setConfirmandoTodasSugestoes] = useState(false)
  const [sugestoesConfirmadas, setSugestoesConfirmadas] = useState(false)

  // ── Fase 0.5: "Revisar duplicatas" — mesma conta/sentido/valor importados duas vezes ─────────
  const [modalDuplicatas, setModalDuplicatas] = useState(false)
  const [duplicatas, setDuplicatas] = useState<DuplicataProvavel[]>([])
  const [carregandoDuplicatas, setCarregandoDuplicatas] = useState(false)
  const [excluindoDuplicataId, setExcluindoDuplicataId] = useState<string | null>(null)

  // ── Investimento: rendimento e vínculo com meta ──────────────────────────
  const [modalRendimento, setModalRendimento] = useState(false)
  const [rendValorDisplay, setRendValorDisplay] = useState('')
  const [rendValor, setRendValor] = useState(0)
  const [rendPositivo, setRendPositivo] = useState(true)
  const [rendData, setRendData] = useState(todayISO())
  const [rendDescricao, setRendDescricao] = useState('')
  const [salvandoRendimento, setSalvandoRendimento] = useState(false)

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
  const [lancamentoParaEditar, setLancamentoParaEditar] = useState<LancamentoExtrato | null>(null)
  const [categoriaEdicao, setCategoriaEdicao] = useState('')
  const [salvandoEdicao, setSalvandoEdicao] = useState(false)

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

  useEffect(() => { listarCategorias().then(setCategorias).catch(() => {}) }, [])

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
    try {
      const resumo = await previewExtrato(contaId, arquivo)
      setDataInicioImport(resumo.dataInicioArquivo || todayISO())
      setDataFimImport(resumo.dataFimArquivo || todayISO())
      setResumoImportacao(resumo)
      setModalImportar(true)
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao ler o arquivo.')
    } finally {
      setPreviewCarregando(false)
    }
  }

  async function handleAlterarIntervaloImportacao(novoInicio: string, novoFim: string) {
    setDataInicioImport(novoInicio)
    setDataFimImport(novoFim)
    if (!contaId || !arquivoImportar || !novoInicio || !novoFim) return
    setPreviewCarregando(true)
    try {
      setResolucoesDuplicatas({})
      setResumoImportacao(await previewExtrato(contaId, arquivoImportar, { dataInicio: novoInicio, dataFim: novoFim }))
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao calcular resumo da importação.')
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
      })
      setResultadoImportacao(resultado)
      setModalImportar(false)
      setArquivoImportar(null)
      setResumoImportacao(null)
      setResolucoesDuplicatas({})
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
    if (!confirm('Desfazer esta classificação como Transferência? O lançamento volta a ficar sem categoria (pendente) — o dinheiro continua lançado, só a classificação muda.')) return
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

  async function handleExcluirLancamento(lancamento: LancamentoExtrato) {
    if (!lancamento.id || !contaId) return
    const avisoTransferencia = lancamento.categoria === 'Transferência'
      ? ' Esse lançamento é uma ponta de Transferência — a contrapartida na outra conta também será excluída.'
      : ' Se ele foi usado para dar baixa em alguma conta a pagar/receber, o título volta a ficar em aberto.'
    if (!confirm(`Excluir "${lancamento.descricao}"? Essa ação não pode ser desfeita.${avisoTransferencia}`)) return
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
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao excluir lançamento.')
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

  async function confirmarEdicaoCategoria() {
    if (!lancamentoParaEditar?.id || !contaId || !categoriaEdicao) return
    setSalvandoEdicao(true)
    setMsg('')
    try {
      await categorizarPendentes(contaId, [{ id: lancamentoParaEditar.id, data: lancamentoParaEditar.data, categoria: categoriaEdicao }])

      // Categoria veio de uma regra e o usuário trocou pra outra — a regra em si NUNCA é alterada
      // sozinha, mas oferece atualizar (só essa reclassificação manual, não muda o critério).
      const regraId = lancamentoParaEditar.regraCategorizacaoId
      if (regraId && categoriaEdicao !== lancamentoParaEditar.categoria && clienteId) {
        try {
          const regras = await listarRegras(clienteId)
          const regra = regras.find(r => r.id === regraId)
          if (regra && regra.acaoTipo === 'Categoria' && regra.categoria !== categoriaEdicao
            && confirm(`Esse lançamento veio da regra baseada em "${regra.descricaoReferencia}" (categoria "${regra.categoria}"). Atualizar a regra para usar "${categoriaEdicao}" daqui pra frente?`)) {
            await atualizarRegra(regra.id, {
              descricaoReferencia: regra.descricaoReferencia,
              acaoTipo: regra.acaoTipo,
              categoria: categoriaEdicao,
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
        (l.id === lancamentoParaEditar.id && l.data === lancamentoParaEditar.data) ? { ...l, categoria: categoriaEdicao } : l))
      setLancamentoParaEditar(null)
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao editar categoria.')
    } finally {
      setSalvandoEdicao(false)
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
    setRendValorDisplay(''); setRendValor(0); setRendPositivo(true); setRendData(todayISO()); setRendDescricao('')
    setModalRendimento(true)
  }

  async function handleSalvarRendimento() {
    if (!contaId || rendValor <= 0) return
    setSalvandoRendimento(true)
    try {
      await registrarRendimento(contaId, {
        data: rendData,
        valor: rendPositivo ? rendValor : -rendValor,
        descricao: rendDescricao || undefined,
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

  const renderPendencia = (item: ContaProvisionada, tipo: 'receber' | 'pagar') => (
    <div key={`${item.descricao}-${item.dataVencimento}-${item.valor}`} className="conta-item">
      <div className="conta-info">
        <div className="conta-desc">{item.descricao}</div>
        <div className="conta-meta">
          {item.dataVencimento ? `Vence: ${fmtDate(item.dataVencimento)}` : 'Sem vencimento'}
          {item.categoria ? ` · ${item.categoria}` : ''}
        </div>
      </div>
      <div className={`conta-valor ${tipo}`}>{fmtBRL(item.valor)}</div>
    </div>
  )

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
          <span className="cd-saldo-label">Saldo atual</span>
          <span className="cd-saldo-val val-green">{fmtBRL(conta.saldoAtual)}</span>
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
            📈 Registrar rendimento
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
            <span className="cb-resumo-val val-green">{fmtBRL(conta.saldoAtual)}</span>
          </div>
          <div className="cb-resumo-item">
            <span className="cb-resumo-label">Rentabilidade no período</span>
            <span className={`cb-resumo-val ${(conta.rentabilidadePercentual ?? 0) >= 0 ? 'val-green' : 'val-red'}`}>
              {conta.rentabilidadePercentual != null ? fmtPct(conta.rentabilidadePercentual) : '—'}
            </span>
          </div>
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
                    <div className="conta-meta">Ano {m.ano} · Alvo: {fmtBRL(m.valorSonho)}</div>
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
          </div>

          {loadingExtrato ? (
            <p style={{ color: 'var(--tx3)' }}>Carregando extrato...</p>
          ) : lancamentos.length === 0 ? (
            <p style={{ color: 'var(--tx3)', fontSize: 13 }}>Nenhum lançamento no período selecionado.</p>
          ) : (
            <div className="cd-extrato-lista">
              <div className="cd-extrato-cabecalho">
                <span>Data</span>
                <span>Descrição</span>
                <span>Categoria</span>
                <span className="cd-col-valor">Valor</span>
                <span className="cd-col-valor">Saldo</span>
              </div>
              {lancamentos.map((l, i) => (
                <div key={`${l.data}-${i}`} className="cd-extrato-linha">
                  <span>{fmtDate(l.data)}</span>
                  <span>
                    {l.descricao}
                    {l.id && l.categoria !== 'Transferência' && (
                      <button
                        type="button"
                        aria-label={`Marcar "${l.descricao}" como transferência`}
                        onClick={() => abrirModalTransferencia(l)}
                        title="Não é receita nem despesa — é uma transferência entre contas"
                        style={{ marginLeft: 6, background: 'none', border: 'none', cursor: 'pointer', fontSize: 11, color: 'var(--tx3)', textDecoration: 'underline', padding: 0 }}
                      >
                        🔁 é transferência?
                      </button>
                    )}
                    {l.categoria === 'Transferência' && l.transferenciaId && (
                      <button
                        type="button"
                        onClick={() => handleDesfazerTransferencia(l.transferenciaId!)}
                        title="Classificado por engano? Volta a ficar sem categoria, pendente"
                        style={{ marginLeft: 6, background: 'none', border: 'none', cursor: 'pointer', fontSize: 11, color: 'var(--tx3)', textDecoration: 'underline', padding: 0 }}
                      >
                        ↩ desfazer classificação
                      </button>
                    )}
                    {l.id && !l.pendenteCategorizacao && l.categoria !== 'Transferência' && (
                      <button
                        type="button"
                        aria-label={`Editar categoria de "${l.descricao}"`}
                        onClick={() => abrirModalEditarCategoria(l)}
                        title="Trocar a categoria deste lançamento"
                        style={{ marginLeft: 6, background: 'none', border: 'none', cursor: 'pointer', fontSize: 11, color: 'var(--tx3)', textDecoration: 'underline', padding: 0 }}
                      >
                        ✏️ editar categoria
                      </button>
                    )}
                    {l.id && (
                      <button
                        type="button"
                        aria-label={`Excluir lançamento ${l.descricao}`}
                        onClick={() => handleExcluirLancamento(l)}
                        title="Excluir este lançamento"
                        style={{ marginLeft: 6, background: 'none', border: 'none', cursor: 'pointer', fontSize: 11, color: 'var(--danger, #e05252)', textDecoration: 'underline', padding: 0 }}
                      >
                        🗑️ excluir
                      </button>
                    )}
                  </span>
                  <span className="cd-categoria">
                    {l.pendenteCategorizacao ? (
                      <span style={{ color: 'var(--warning)' }} title="Sem categoria — afeta o saldo normalmente, só falta classificar">
                        🏷️ Pendente
                      </span>
                    ) : (
                      <>
                        {l.categoria || '—'}
                        {l.regraCategorizacaoId && (
                          <span title="Categorizado automaticamente por uma regra" style={{ marginLeft: 4, fontSize: 11, color: 'var(--tx3)' }}>⚙️</span>
                        )}
                      </>
                    )}
                  </span>
                  <span className={`cd-col-valor ${l.valor >= 0 ? 'val-green' : 'val-red'}`}>
                    {l.valor >= 0 ? '+' : ''}{fmtBRL(l.valor)}
                  </span>
                  <span className="cd-col-valor">{fmtBRL(l.saldoAcumulado)}</span>
                </div>
              ))}
            </div>
          )}
        </>
      )}

      {aba === 'recebiveis' && (
        loadingPendencias ? <p style={{ color: 'var(--tx3)' }}>Carregando...</p> :
        recebiveis.length === 0 ? <p style={{ color: 'var(--tx3)', fontSize: 13 }}>Nenhum recebível pendente vinculado a esta conta.</p> :
        <div className="contas-section">{recebiveis.map(r => renderPendencia(r, 'receber'))}</div>
      )}

      {aba === 'pagamentos' && (
        loadingPendencias ? <p style={{ color: 'var(--tx3)' }}>Carregando...</p> :
        pagamentos.length === 0 ? <p style={{ color: 'var(--tx3)', fontSize: 13 }}>Nenhum pagamento pendente vinculado a esta conta.</p> :
        <div className="contas-section">{pagamentos.map(p => renderPendencia(p, 'pagar'))}</div>
      )}

      <Modal
        open={modalRendimento}
        title="📈 Registrar rendimento"
        onClose={() => setModalRendimento(false)}
        footer={
          <>
            <button className="btn-cancel" onClick={() => setModalRendimento(false)}>Cancelar</button>
            <button className="btn-confirm" onClick={handleSalvarRendimento} disabled={rendValor <= 0 || salvandoRendimento}>
              {salvandoRendimento ? 'Salvando...' : 'Registrar'}
            </button>
          </>
        }
      >
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
              disabled={importando || previewCarregando || !resumoImportacao || resumoImportacao.totalNovas === 0}
            >
              {importando ? 'Importando...' : `Importar ${resumoImportacao?.totalNovas ?? 0} transação(ões)`}
            </button>
          </>
        }
      >
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
              {resumoImportacao.totalNovas > 0 && (
                <> · <span className="val-green">+{fmtBRL(resumoImportacao.totalEntradas)}</span> em entradas · <span className="val-red">-{fmtBRL(resumoImportacao.totalSaidas)}</span> em saídas</>
              )}.
            </p>
            {resumoImportacao.totalNovas > 0 && (
              <p style={{ fontSize: 12 }}>Entram como <strong>pendentes de categorização</strong> — categorize depois na tela de categorização.</p>
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
    </>
  )
}
