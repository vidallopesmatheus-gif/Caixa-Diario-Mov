import { useState, useMemo, useEffect } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { useRegistros } from '../../hooks/useRegistros'
import { fmtBRL, fmtDate, todayISO, addDays } from '../../utils/format'
import {
  listarContasRecorrentes, criarContaRecorrente, atualizarContaRecorrente, desativarContaRecorrente,
} from '../../api/contasRecorrentes'
import { listarContasBancarias } from '../../api/contasBancarias'
import { criarContaProvisionada, atualizarContaProvisionada, excluirContaProvisionada } from '../../api/contasProvisionadas'
import { listarSugestoesVinculo, ignorarSugestaoVinculo, type SugestaoVinculo } from '../../api/conciliacao'
import Modal from '../../components/shared/Modal'
import type { ContaProvisionada, ContaRecorrente, ContaBancaria } from '../../types'
import './ClientContas.css'
import './ClientContasBancarias.css'

interface Props { clienteIdOverride?: string }

interface ContaView {
  registroData: string
  // Id do RegistroDiario onde esta ContaProvisionada realmente vive — identifica sem ambiguidade
  // qual registro editar/salvar quando existe mais de um na mesma data (uma por conta bancária
  // diferente). Usar (data, contaBancariaId) como antes falhava quando contaBancariaId vinha
  // undefined em mais de um registro da mesma data — o id é sempre único.
  registroId: string
  tipo: 'receber' | 'pagar'
  index: number
  conta: ContaProvisionada
}

interface LancamentoEncontrado {
  id: string
  descricao: string
  valor: number
}

function fmtNum(n: number) {
  if (!n) return ''
  return n.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}
function parseBRL(s: string): number {
  return parseFloat(s.replace(/\./g, '').replace(',', '.')) || 0
}

export default function ClientContasPage({ clienteIdOverride }: Props) {
  const { user } = useAuth()
  const clienteId = clienteIdOverride ?? user?.usuarioId ?? null
  const { registros, salvar, recarregar, loading } = useRegistros(clienteId)

  // Formulário unificado
  const [tipo, setTipo] = useState<'receber' | 'pagar'>('receber')
  const [desc, setDesc] = useState('')
  const [valorDisplay, setValorDisplay] = useState('')
  const [valor, setValor] = useState(0)
  const [venc, setVenc] = useState(todayISO())
  const [isRecorrente, setIsRecorrente] = useState(false)
  const [recInicio, setRecInicio] = useState(todayISO())
  const [recFim, setRecFim] = useState('')
  const [recPeriodicidade, setRecPeriodicidade] = useState('Mensal')
  const [recParcelas, setRecParcelas] = useState('')
  const [recValorVariavel, setRecValorVariavel] = useState(false)
  const [recDiaVencimento, setRecDiaVencimento] = useState('')
  const [saving, setSaving] = useState(false)
  const [msg, setMsg] = useState('')
  const [msgOk, setMsgOk] = useState(true)

  const [recorrentes, setRecorrentes] = useState<ContaRecorrente[]>([])
  const [contasBancarias, setContasBancarias] = useState<ContaBancaria[]>([])
  const [contaSelecionadaId, setContaSelecionadaId] = useState('')
  const [showDuplicateModal, setShowDuplicateModal] = useState(false)
  const [pendingDuplicate, setPendingDuplicate] = useState<{
    tipo: 'receber' | 'pagar'
    conta: ContaProvisionada
    registroData: string
  } | null>(null)

  // ── Baixa (marcar como recebido/pago): permite trocar a conta e evita lançamento duplicado ──
  const [modalBaixa, setModalBaixa] = useState(false)
  const [baixaView, setBaixaView] = useState<ContaView | null>(null)
  const [baixaContaId, setBaixaContaId] = useState('')
  const [baixaData, setBaixaData] = useState(todayISO())
  const [baixaValorDisplay, setBaixaValorDisplay] = useState('')
  const [baixaValor, setBaixaValor] = useState(0)
  const [confirmandoBaixa, setConfirmandoBaixa] = useState(false)

  // ── Estornar baixa: sempre pede confirmação e avisa o impacto no saldo. Se `paraEditar` for
  // true, ao confirmar a conta some da baixa e o formulário de edição abre em seguida.
  const [estornoView, setEstornoView] = useState<ContaView | null>(null)
  const [estornoParaEditar, setEstornoParaEditar] = useState(false)
  const [estornando, setEstornando] = useState(false)

  // ── Editar conta pendente (a pagar/receber) ──────────────────────────────────────────────
  const [editView, setEditView] = useState<ContaView | null>(null)
  const [editDesc, setEditDesc] = useState('')
  const [editValorDisplay, setEditValorDisplay] = useState('')
  const [editValor, setEditValor] = useState(0)
  const [editVenc, setEditVenc] = useState('')
  const [editContaId, setEditContaId] = useState('')
  const [salvandoEdit, setSalvandoEdit] = useState(false)

  // ── Excluir conta pendente ────────────────────────────────────────────────────────────────
  const [excluirView, setExcluirView] = useState<ContaView | null>(null)
  const [excluindo, setExcluindo] = useState(false)

  // ── Editar recorrência ────────────────────────────────────────────────────────────────────
  const [editRec, setEditRec] = useState<ContaRecorrente | null>(null)
  const [editRecValorDisplay, setEditRecValorDisplay] = useState('')
  const [editRecValor, setEditRecValor] = useState(0)
  const [editRecPeriodicidade, setEditRecPeriodicidade] = useState('Mensal')
  const [editRecInicio, setEditRecInicio] = useState('')
  const [editRecFim, setEditRecFim] = useState('')
  const [editRecContaId, setEditRecContaId] = useState('')
  const [editRecValorVariavel, setEditRecValorVariavel] = useState(false)
  const [editRecDiaVencimento, setEditRecDiaVencimento] = useState('')
  // '' força o usuário a escolher — nunca decide silenciosamente o que acontece com as já geradas.
  const [editRecAlcance, setEditRecAlcance] = useState<'' | 'futuras' | 'todas'>('')
  const [salvandoEditRec, setSalvandoEditRec] = useState(false)

  // ── Excluir recorrência ───────────────────────────────────────────────────────────────────
  const [excluirRec, setExcluirRec] = useState<ContaRecorrente | null>(null)
  const [excluirRecPendentes, setExcluirRecPendentes] = useState<'' | 'manter' | 'remover'>('')
  const [excluindoRec, setExcluindoRec] = useState(false)

  // ── Fase 1.2/1.3: sugestões de vínculo (título pendente × lançamento já importado) ──────────
  const [sugestoes, setSugestoes] = useState<SugestaoVinculo[]>([])
  const [sugestoesIgnoradas, setSugestoesIgnoradas] = useState<Set<string>>(new Set())
  const [vinculandoSugestaoId, setVinculandoSugestaoId] = useState<string | null>(null)

  useEffect(() => {
    if (!clienteId) return
    listarContasRecorrentes(clienteId).then(setRecorrentes).catch(console.error)
    listarContasBancarias(clienteId).then(setContasBancarias).catch(console.error)
  }, [clienteId])

  useEffect(() => {
    if (!clienteId) return
    const de = addDays(todayISO(), -90)
    const ate = addDays(todayISO(), 90)
    listarSugestoesVinculo(clienteId, de, ate).then(setSugestoes).catch(console.error)
    // Re-busca sempre que `registros` mudar (toda baixa/import/edição chama recarregar()) — sem
    // cache, igual ao plano da Fase 1.2.
  }, [clienteId, registros])

  // Item 3.3: chave é o PAR título×lançamento (não só o título) — igual ao que o backend persiste,
  // já que um título pode (em tese) receber mais de uma sugestão em buscas diferentes.
  const chaveSugestao = (s: SugestaoVinculo) => `${s.contaProvisionadaId}::${s.lancamentoId}`

  const sugestoesVisiveis = useMemo(
    () => sugestoes.filter(s => !sugestoesIgnoradas.has(chaveSugestao(s))),
    [sugestoes, sugestoesIgnoradas],
  )

  async function vincularSugestao(s: SugestaoVinculo) {
    if (!clienteId) return
    setVinculandoSugestaoId(s.contaProvisionadaId)
    try {
      await atualizarContaProvisionada(clienteId, s.contaProvisionadaId, {
        pago: true, contaBancariaId: s.contaBancariaId, dataPagamento: s.lancamentoData, lancamentoVinculadoId: s.lancamentoId,
      })
      await recarregar()
      setMsg('Baixa vinculada ao lançamento sugerido.')
      setMsgOk(true)
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : String(e))
      setMsgOk(false)
    } finally {
      setVinculandoSugestaoId(null)
    }
  }

  // Item 3.3: persiste a decisão no backend — sem isso, a mesma sugestão reaparecia em toda nova
  // busca (a tela re-busca a cada import/baixa). Some da tela já (Set local) sem esperar a rede.
  function ignorarSugestao(s: SugestaoVinculo) {
    setSugestoesIgnoradas(prev => new Set(prev).add(chaveSugestao(s)))
    if (clienteId) ignorarSugestaoVinculo(clienteId, s.contaProvisionadaId, s.lancamentoId).catch(console.error)
  }

  useEffect(() => {
    if (!contasBancarias.length) return
    if (!contaSelecionadaId) {
      const caixa = contasBancarias.find(c => c.tipo === 'Caixa' || c.nome.toLowerCase() === 'caixa') ?? contasBancarias[0]
      setContaSelecionadaId(caixa.id)
    }
  }, [contasBancarias, contaSelecionadaId])

  // Item 2.5: depois de adicionar, o formulário volta ao padrão da TELA (tipo "Receber" e a
  // conta padrão, normalmente o Caixa) — antes ficava com o último tipo/conta escolhidos, o que
  // já causou lançamento na conta errada quando o usuário esquecia de trocar de volta.
  function resetForm() {
    setDesc(''); setValorDisplay(''); setValor(0); setVenc(todayISO())
    setRecInicio(todayISO()); setRecFim(''); setRecPeriodicidade('Mensal'); setRecParcelas('')
    setRecValorVariavel(false); setRecDiaVencimento('')
    setTipo('receber')
    if (contasBancarias.length > 0) {
      const caixa = contasBancarias.find(c => c.tipo === 'Caixa' || c.nome.toLowerCase() === 'caixa') ?? contasBancarias[0]
      setContaSelecionadaId(caixa.id)
    }
  }

  function encontrarDuplicata(conta: ContaProvisionada, registroData: string) {
    const dataReferencia = conta.dataVencimento || registroData
    return todasContas.find(view => {
      if (view.tipo !== tipo) return false
      const mesmaData = (view.conta.dataVencimento || view.registroData) === dataReferencia
      const mesmoValor = Math.abs(view.conta.valor - conta.valor) < 0.01
      const mesmaDescricao = view.conta.descricao.trim().toLowerCase() === conta.descricao.trim().toLowerCase()
      const mesmaConta = !conta.contaBancariaId || !view.conta.contaBancariaId || view.conta.contaBancariaId === conta.contaBancariaId
      return mesmaData && mesmoValor && mesmaDescricao && mesmaConta
    })
  }

  async function handleAdicionar() {
    if (!clienteId || !desc || !valor) return
    setSaving(true)
    setMsg('')
    try {
      if (isRecorrente) {
        if (!recInicio) { setMsg('Informe a data de início.'); setMsgOk(false); return }
        if (!contaSelecionadaId) { setMsg('Cadastre uma conta bancária em Configurações antes de continuar.'); setMsgOk(false); return }
        const nova = await criarContaRecorrente({
          clienteId, descricao: desc, valor,
          tipo: tipo === 'receber' ? 'Receber' : 'Pagar',
          dataInicio: recInicio, dataFim: recFim || undefined,
          periodicidade: recPeriodicidade,
          quantidadeParcelas: recParcelas ? Number(recParcelas) : undefined,
          contaBancariaId: contaSelecionadaId,
          valorVariavel: recValorVariavel,
          diaVencimento: recDiaVencimento ? Number(recDiaVencimento) : undefined,
        })
        setRecorrentes(prev => [...prev, nova])
        // O backend materializa a 1ª ocorrência do mês atual ao listar registros (ver
        // RegistroService.ListarPorClienteAsync) — sem recarregar aqui, ela só aparecia com F5.
        await recarregar()
        setMsg('Conta recorrente adicionada!')
        setMsgOk(true)
      } else {
        if (!contaSelecionadaId) { setMsg('Cadastre uma conta bancária em Configurações antes de continuar.'); setMsgOk(false); return }
        const hoje = todayISO()
        const novaConta: ContaProvisionada = {
          descricao: desc,
          valor,
          dataVencimento: venc || undefined,
          pago: false,
          contaBancariaId: contaSelecionadaId,
        }
        const duplicata = encontrarDuplicata(novaConta, hoje)
        if (duplicata) {
          setPendingDuplicate({ tipo, conta: novaConta, registroData: hoje })
          setShowDuplicateModal(true)
          return
        }
        // Fase 0.4: endpoint dedicado — não reenvia o RegistroDiario inteiro (que encontra/cria
        // sozinho, do lado do backend, usando o saldo anterior real da conta como ponto de partida).
        await criarContaProvisionada({
          clienteId, contaBancariaId: contaSelecionadaId, tipo: tipo === 'receber' ? 'Receber' : 'Pagar',
          descricao: desc, valor, dataVencimento: venc || undefined,
        })
        await recarregar()
        setMsg('Conta adicionada!')
        setMsgOk(true)
      }
      resetForm()
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : String(e))
      setMsgOk(false)
    } finally {
      setSaving(false)
    }
  }

  const todasContas = useMemo<ContaView[]>(() => {
    const acc: ContaView[] = []
    for (const reg of registros) {
      reg.contasAReceber.forEach((c, i) => acc.push({ registroData: reg.data, registroId: reg.id, tipo: 'receber', index: i, conta: c }))
      reg.contasAPagar.forEach((c, i) => acc.push({ registroData: reg.data, registroId: reg.id, tipo: 'pagar', index: i, conta: c }))
    }
    return acc.sort((a, b) => {
      const da = a.conta.dataVencimento ?? a.registroData
      const db = b.conta.dataVencimento ?? b.registroData
      return da.localeCompare(db)
    })
  }, [registros])

  // Item 2.5: filtro por período (vencimento, ou data de lançamento quando não tem vencimento) e
  // por conta bancária — '' em cada um significa "sem filtro" (mostra tudo).
  const [filtroContaId, setFiltroContaId] = useState('')
  const [filtroDe, setFiltroDe] = useState('')
  const [filtroAte, setFiltroAte] = useState('')

  const contasFiltradas = useMemo(() => todasContas.filter(view => {
    if (filtroContaId && view.conta.contaBancariaId !== filtroContaId) return false
    const dataRef = view.conta.dataVencimento ?? view.registroData
    if (filtroDe && dataRef < filtroDe) return false
    if (filtroAte && dataRef > filtroAte) return false
    return true
  }), [todasContas, filtroContaId, filtroDe, filtroAte])

  const pendentesReceber = contasFiltradas.filter(c => c.tipo === 'receber' && !c.conta.pago)
  const recebidasList = contasFiltradas.filter(c => c.tipo === 'receber' && c.conta.pago)
  const pendentesPagar = contasFiltradas.filter(c => c.tipo === 'pagar' && !c.conta.pago)
  const pagasList = contasFiltradas.filter(c => c.tipo === 'pagar' && c.conta.pago)
  const totalPendenteReceber = pendentesReceber.reduce((s, v) => s + v.conta.valor, 0)
  const totalPendentePagar = pendentesPagar.reduce((s, v) => s + v.conta.valor, 0)
  const contaSelecionada = contasBancarias.find(c => c.id === contaSelecionadaId)

  function origemRegistro(view: ContaView) {
    return registros.find(r => r.id === view.registroId)
  }

  // Fallback só pra itens legados sem Id (nunca resalvos desde a Fase 0.4) — todo item novo usa
  // os endpoints dedicados (criar/atualizar/excluirContaProvisionada) acima, sem passar por aqui.
  async function persistirListas(view: ContaView, transformar: (contas: ContaProvisionada[]) => ContaProvisionada[]) {
    if (!clienteId) return
    const reg = origemRegistro(view)
    // Nunca falha em silêncio: sem isso, a tela mostrava "sucesso" mesmo quando o registro de
    // origem não era encontrado (ex.: excluído em outra aba) e nada era salvo de verdade.
    if (!reg) throw new Error('Não foi possível localizar o registro original desta conta — atualize a página e tente de novo.')
    // Registro "sem conta" (legado): nunca reenviar contaBancariaId undefined/null — cai pro
    // default da tela em vez de perpetuar um registro sem conta vinculada.
    const contaBancariaId = reg.contaBancariaId ?? contaSelecionadaId
    if (!contaBancariaId) throw new Error('Cadastre uma conta bancária em Configurações antes de continuar.')
    await salvar({
      clienteId, contaBancariaId, data: reg.data, saldoInicio: reg.saldoInicio,
      entradas: reg.entradas, saidas: reg.saidas,
      contasAReceber: view.tipo === 'receber' ? transformar(reg.contasAReceber) : reg.contasAReceber,
      contasAPagar: view.tipo === 'pagar' ? transformar(reg.contasAPagar) : reg.contasAPagar,
      saldoConfirmado: reg.saldoConfirmado,
    })
  }

  async function desfazerBaixa(view: ContaView) {
    if (!clienteId) return
    // Fase 0.4: por Id quando disponível — desfaz só o lançamento que a própria baixa criou,
    // nunca um que já existia e só foi vinculado (ver ContaProvisionadaService.EstornarAsync).
    if (view.conta.id) {
      await atualizarContaProvisionada(clienteId, view.conta.id, { pago: false })
      await recarregar()
      return
    }
    await persistirListas(view, contas => contas.map((c, i) =>
      i === view.index ? { ...c, pago: false, dataBaixa: undefined, valorRealizado: undefined, lancamentoVinculadoId: undefined } : c))
  }

  function togglePago(view: ContaView) {
    setBaixaView(view)
    setBaixaContaId(view.conta.contaBancariaId || contaSelecionadaId)
    setBaixaData(todayISO())
    setBaixaValor(view.conta.valor)
    setBaixaValorDisplay(fmtNum(view.conta.valor))
    setModalBaixa(true)
  }

  // Lançamento real (Entrada se receber, Saída se pagar) na conta+data escolhidas na baixa,
  // com o mesmo valor — indício de que esse dinheiro já foi lançado por outro caminho.
  const lancamentoDuplicado = useMemo<LancamentoEncontrado | null>(() => {
    if (!baixaView) return null
    const regDaConta = registros.find(r => r.data === baixaView.registroData && r.contaBancariaId === baixaContaId)
    if (!regDaConta) return null
    const itens = baixaView.tipo === 'receber' ? regDaConta.entradas : regDaConta.saidas
    const item = itens.find(i => Math.abs(i.valor - baixaView.conta.valor) < 0.01)
    return item ? { id: item.id ?? '', descricao: item.descricao, valor: item.valor } : null
  }, [baixaView, baixaContaId, registros])

  async function confirmarBaixa(vincular: boolean) {
    if (!baixaView || !clienteId) return
    setConfirmandoBaixa(true)
    try {
      const lancamentoVinculadoId = vincular && lancamentoDuplicado?.id ? lancamentoDuplicado.id : undefined
      // valorRealizado só vai além de undefined quando difere do valor do título — mantém o Valor
      // original intacto (é ele que identifica o item em futuras edições).
      const valorRealizado = Math.abs(baixaValor - baixaView.conta.valor) >= 0.01 ? baixaValor : undefined
      if (baixaView.conta.id) {
        // Fase 0.5: sempre gera um lançamento real (ou vincula a um existente) na conta+data do
        // pagamento — nunca mais um ajuste de saldo "fantasma" sem contrapartida no extrato.
        await atualizarContaProvisionada(clienteId, baixaView.conta.id, {
          pago: true, contaBancariaId: baixaContaId, dataPagamento: baixaData, valorRealizado, lancamentoVinculadoId,
        })
        await recarregar()
      } else {
        await persistirListas(baixaView, contas => contas.map((c, i) =>
          i === baixaView.index
            ? { ...c, pago: true, contaBancariaId: baixaContaId, dataBaixa: baixaData, valorRealizado, lancamentoVinculadoId }
            : c))
      }
      setMsg(vincular ? 'Baixa vinculada ao lançamento existente.' : 'Baixa confirmada.')
      setMsgOk(true)
      setModalBaixa(false)
      setBaixaView(null)
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : String(e))
      setMsgOk(false)
    } finally {
      setConfirmandoBaixa(false)
    }
  }

  // ── Estornar baixa (sempre com confirmação explícita do impacto no saldo) ──────────────────
  function abrirEstorno(view: ContaView, paraEditar: boolean) {
    setEstornoView(view)
    setEstornoParaEditar(paraEditar)
  }

  function abrirEdicaoConta(view: ContaView) {
    setEditView(view)
    setEditDesc(view.conta.descricao)
    setEditValor(view.conta.valor)
    setEditValorDisplay(fmtNum(view.conta.valor))
    setEditVenc(view.conta.dataVencimento ?? '')
    setEditContaId(view.conta.contaBancariaId ?? contaSelecionadaId)
  }

  async function confirmarEstorno() {
    if (!estornoView) return
    setEstornando(true)
    try {
      await desfazerBaixa(estornoView)
      if (estornoParaEditar) abrirEdicaoConta({ ...estornoView, conta: { ...estornoView.conta, pago: false } })
      setMsg('Baixa estornada — conta voltou a ficar pendente.')
      setMsgOk(true)
      setEstornoView(null)
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : String(e))
      setMsgOk(false)
    } finally {
      setEstornando(false)
    }
  }

  // ── Editar conta pendente ────────────────────────────────────────────────────────────────
  async function confirmarEdicaoConta() {
    if (!editView || !editDesc.trim() || !editValor || !clienteId) return
    setSalvandoEdit(true)
    try {
      if (editView.conta.id) {
        await atualizarContaProvisionada(clienteId, editView.conta.id, {
          descricao: editDesc.trim(), valor: editValor, dataVencimento: editVenc || undefined, contaBancariaId: editContaId || undefined,
        })
        await recarregar()
      } else {
        await persistirListas(editView, contas => contas.map((c, i) => i === editView.index
          ? { ...c, descricao: editDesc.trim(), valor: editValor, dataVencimento: editVenc || undefined, contaBancariaId: editContaId || undefined }
          : c))
      }
      setMsg('Conta atualizada!')
      setMsgOk(true)
      setEditView(null)
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : String(e))
      setMsgOk(false)
    } finally {
      setSalvandoEdit(false)
    }
  }

  // ── Excluir conta pendente ───────────────────────────────────────────────────────────────
  async function confirmarExclusaoConta() {
    if (!excluirView || !clienteId) return
    setExcluindo(true)
    try {
      if (excluirView.conta.id) {
        await excluirContaProvisionada(clienteId, excluirView.conta.id)
        await recarregar()
      } else {
        await persistirListas(excluirView, contas => contas.filter((_, i) => i !== excluirView.index))
      }
      setMsg('Conta excluída.')
      setMsgOk(true)
      setExcluirView(null)
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : String(e))
      setMsgOk(false)
    } finally {
      setExcluindo(false)
    }
  }

  // ── Editar recorrência ───────────────────────────────────────────────────────────────────
  function abrirEdicaoRecorrencia(r: ContaRecorrente) {
    setEditRec(r)
    setEditRecValor(r.valor)
    setEditRecValorDisplay(fmtNum(r.valor))
    setEditRecPeriodicidade(r.periodicidade)
    setEditRecInicio(r.dataInicio)
    setEditRecFim(r.dataFim ?? '')
    setEditRecContaId(r.contaBancariaId ?? '')
    setEditRecValorVariavel(r.valorVariavel)
    setEditRecDiaVencimento(r.diaVencimento ? String(r.diaVencimento) : '')
    setEditRecAlcance('')
  }

  async function confirmarEdicaoRecorrencia() {
    if (!editRec || !clienteId || !editRecAlcance || !editRecValor) return
    setSalvandoEditRec(true)
    try {
      const atualizada = await atualizarContaRecorrente(clienteId, editRec.id, {
        valor: editRecValor,
        periodicidade: editRecPeriodicidade,
        dataInicio: editRecInicio,
        dataFim: editRecFim || undefined,
        contaBancariaId: editRecContaId || undefined,
        valorVariavel: editRecValorVariavel,
        diaVencimento: editRecDiaVencimento ? Number(editRecDiaVencimento) : undefined,
        aplicarAsPendentes: editRecAlcance === 'todas',
      })
      setRecorrentes(prev => prev.map(r => r.id === atualizada.id ? atualizada : r))
      if (editRecAlcance === 'todas') await recarregar()
      setMsg('Recorrência atualizada!')
      setMsgOk(true)
      setEditRec(null)
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : String(e))
      setMsgOk(false)
    } finally {
      setSalvandoEditRec(false)
    }
  }

  // ── Excluir recorrência ──────────────────────────────────────────────────────────────────
  function abrirExclusaoRecorrencia(r: ContaRecorrente) {
    setExcluirRec(r)
    setExcluirRecPendentes('')
  }

  async function confirmarExclusaoRecorrencia() {
    if (!excluirRec || !clienteId || !excluirRecPendentes) return
    setExcluindoRec(true)
    try {
      const removerPendentes = excluirRecPendentes === 'remover'
      await desativarContaRecorrente(clienteId, excluirRec.id, removerPendentes)
      setRecorrentes(prev => prev.filter(r => r.id !== excluirRec.id))
      if (removerPendentes) await recarregar()
      setMsg('Recorrência excluída.')
      setMsgOk(true)
      setExcluirRec(null)
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : String(e))
      setMsgOk(false)
    } finally {
      setExcluindoRec(false)
    }
  }

  async function confirmarDuplicata(linkToExisting: boolean) {
    if (!pendingDuplicate || !clienteId) return
    const contaAlvo = pendingDuplicate.conta.contaBancariaId || contaSelecionadaId

    try {
      if (!linkToExisting) {
        await criarContaProvisionada({
          clienteId, contaBancariaId: contaAlvo, tipo: pendingDuplicate.tipo === 'receber' ? 'Receber' : 'Pagar',
          descricao: pendingDuplicate.conta.descricao, valor: pendingDuplicate.conta.valor,
          dataVencimento: pendingDuplicate.conta.dataVencimento,
        })
        await recarregar()
        setMsg('Nova conta criada.')
      } else {
        setMsg('Conta semelhante já existente; nenhuma nova entrada foi criada.')
      }
      setMsgOk(true)
      setShowDuplicateModal(false)
      setPendingDuplicate(null)
      resetForm()
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : String(e))
      setMsgOk(false)
    }
  }

  if (loading) return <p style={{ color: 'var(--tx3)' }}>Carregando...</p>

  const contaCaixaPadrao = contasBancarias.find(c => c.tipo === 'Caixa') ?? contasBancarias[0]

  // Estimativa do impacto no saldo ao estornar — baixa vinculada a um lançamento existente nunca
  // mexeu no saldo (o dinheiro já estava contado por aquele lançamento), então estornar também não mexe.
  function impactoEstorno(view: ContaView): string {
    if (view.conta.lancamentoVinculadoId) {
      return 'Essa baixa está vinculada a um lançamento existente — estornar não muda o saldo, só volta a conta pra pendente e desfaz o vínculo.'
    }
    const sinal = view.tipo === 'receber' ? '−' : '+'
    return `O saldo do dia da baixa vai mudar em ${sinal}${fmtBRL(view.conta.valor)}.`
  }

  const renderConta = (view: ContaView) => {
    // Contas cadastradas antes de existir esse campo (contaBancariaId nulo) resolvem pra Caixa —
    // nunca aparecem sem conta nenhuma.
    const contaNome = contasBancarias.find(c => c.id === view.conta.contaBancariaId)?.nome ?? contaCaixaPadrao?.nome ?? 'Conta padrão'

    // Item 2.5: vencida = pendente com vencimento antes de hoje. diasAtraso sempre >= 1 aqui
    // (vencimento hoje ainda não está atrasado).
    const diasAtraso = !view.conta.pago && view.conta.dataVencimento && view.conta.dataVencimento < todayISO()
      ? Math.round((new Date(todayISO() + 'T12:00:00').getTime() - new Date(view.conta.dataVencimento + 'T12:00:00').getTime()) / 86400000)
      : 0
    const vencida = diasAtraso > 0

    return (
    <div key={`${view.registroId}-${view.tipo}-${view.index}`} className={`conta-item ${view.conta.pago ? 'pago' : ''} ${vencida ? 'vencida' : ''}`}>
      <button
        onClick={() => view.conta.pago ? abrirEstorno(view, false) : togglePago(view)}
        style={{ padding: '4px 12px', borderRadius: 6, border: 'none', cursor: 'pointer', fontSize: 12, fontWeight: 600,
          background: view.conta.pago ? 'var(--bd)' : view.tipo === 'receber' ? '#34c759' : '#ff3b30',
          color: view.conta.pago ? 'var(--tx3)' : '#fff' }}>
        {view.conta.pago
          ? `✓ ${view.tipo === 'receber' ? 'Recebido' : 'Pago'}${view.conta.dataBaixa ? ` em ${fmtDate(view.conta.dataBaixa)}` : ''}`
          : view.tipo === 'receber' ? 'Receber' : 'Pagar'}
      </button>
      <div className="conta-info">
        <div className="conta-desc">{view.conta.descricao}</div>
        <div className="conta-meta">
          Lançado em {fmtDate(view.registroData)}
          {view.conta.dataVencimento ? ` · Vence: ${fmtDate(view.conta.dataVencimento)}` : ''}
          {` · Conta: ${contaNome}`}
          {vencida && <span className="conta-atrasada"> · atrasada há {diasAtraso} dia{diasAtraso > 1 ? 's' : ''}</span>}
        </div>
      </div>
      <div className={`conta-valor ${view.tipo}`}>
        {view.conta.pago && view.conta.valorRealizado != null && Math.abs(view.conta.valorRealizado - view.conta.valor) >= 0.01 ? (
          <>
            <span style={{ textDecoration: 'line-through', opacity: 0.6, fontSize: '0.85em', marginRight: 6 }}>
              {fmtBRL(view.conta.valor)}
            </span>
            {fmtBRL(view.conta.valorRealizado)}
          </>
        ) : fmtBRL(view.conta.valor)}
      </div>
      <div className="conta-item-acoes">
        <button
          className="cb-btn-editar"
          onClick={() => view.conta.pago ? abrirEstorno(view, true) : abrirEdicaoConta(view)}
          title={view.conta.pago ? 'Estornar a baixa e editar' : 'Editar'}
        >
          Editar
        </button>
        {!view.conta.pago && (
          <button className="cb-btn-inativar" onClick={() => setExcluirView(view)} title="Excluir">
            Excluir
          </button>
        )}
      </div>
    </div>
    )
  }

  // Conta bancária agora é obrigatória pra QUALQUER lançamento, recorrente ou não (Fase 0.2) —
  // antes, isRecorrente pulava essa checagem e deixava nascer recorrência sem conta vinculada.
  const podeAdicionar = !!desc && valor > 0 && (!isRecorrente || !!recInicio) && !!contaSelecionadaId

  return (
    <>
      {/* Formulário unificado */}
      <div className="add-conta-form">
        <h4>＋ Nova Conta</h4>

        <div className="conta-form-row">
          <div className="conta-field" style={{ flex: '0 1 130px', minWidth: 110 }}>
            <label className="conta-field-label">Tipo</label>
            <select value={tipo} onChange={e => setTipo(e.target.value as 'receber' | 'pagar')} style={{ width: '100%' }}>
              <option value="receber">A Receber</option>
              <option value="pagar">A Pagar</option>
            </select>
          </div>
          <div className="conta-field" style={{ flex: 1.3, minWidth: 160 }}>
            <label className="conta-field-label">Descrição</label>
            <input placeholder="Ex: Aluguel, Cliente X..." value={desc} onChange={e => setDesc(e.target.value)} style={{ width: '100%' }} />
          </div>
          <div className="conta-field" style={{ minWidth: 200, flex: 1.2 }}>
            <label className="conta-field-label">Conta bancária</label>
            <select value={contaSelecionadaId} onChange={e => setContaSelecionadaId(e.target.value)} style={{ width: '100%' }}>
              {contasBancarias.filter(c => c.ativa).map(c => (
                <option key={c.id} value={c.id}>{c.nome}</option>
              ))}
            </select>
            {contaSelecionada && (
              <div className="conta-field-hint">
                {isRecorrente ? `Cada ocorrência será gerada no registro de ${contaSelecionada.nome}.` : `O lançamento será vinculado a ${contaSelecionada.nome}.`}
              </div>
            )}
          </div>
          <div className="conta-field" style={{ flex: 1, minWidth: 140 }}>
            <label className="conta-field-label">Valor</label>
            <div className="val-input-wrap">
              <span className="val-prefix">R$</span>
              <input
                type="text" inputMode="decimal" placeholder="0,00"
                value={valorDisplay}
                onChange={e => {
                  const raw = e.target.value.replace(/[^\d,]/g, '')
                  setValorDisplay(raw)
                  setValor(parseBRL(raw))
                }}
                onBlur={() => setValorDisplay(valor ? fmtNum(valor) : '')}
              />
            </div>
          </div>
          {!isRecorrente && (
            <div className="conta-field" style={{ flex: 1.1, minWidth: 150 }}>
              <label className="conta-field-label">Vencimento</label>
              <input type="date" value={venc} onChange={e => setVenc(e.target.value)} style={{ width: '100%' }} />
              <div className="conta-date-shortcuts">
                {[{ label: 'Hoje', dias: 0 }, { label: '+7d', dias: 7 }, { label: '+30d', dias: 30 }].map(a => (
                  <button
                    key={a.label}
                    type="button"
                    onClick={() => setVenc(addDays(todayISO(), a.dias))}
                  >
                    {a.label}
                  </button>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* Toggle recorrente */}
        <label className="rec-toggle">
          <input type="checkbox" checked={isRecorrente} onChange={e => setIsRecorrente(e.target.checked)} />
          <span className="rec-toggle-track">
            <span className="rec-toggle-thumb" />
          </span>
          <span className="rec-toggle-label">🔁 Recorrente</span>
        </label>

        {/* Opções de recorrência */}
        {isRecorrente && (
          <div className="rec-options">
            <div className="conta-form-row">
              <div style={{ flex: 1 }}>
                <label className="rec-field-label">Início</label>
                <input type="date" value={recInicio} onChange={e => setRecInicio(e.target.value)}
                  className="rec-field-input" />
              </div>
              <div style={{ flex: 1 }}>
                <label className="rec-field-label">Fim (opcional)</label>
                <input type="date" value={recFim} onChange={e => setRecFim(e.target.value)}
                  className="rec-field-input" />
              </div>
              <div style={{ flex: 1 }}>
                <label className="rec-field-label">Periodicidade</label>
                <select value={recPeriodicidade} onChange={e => setRecPeriodicidade(e.target.value)}
                  className="rec-field-input">
                  <option value="Mensal">Mensal</option>
                  <option value="Semanal">Semanal</option>
                  <option value="Quinzenal">Quinzenal</option>
                  <option value="Trimestral">Trimestral</option>
                  <option value="Semestral">Semestral</option>
                  <option value="Anual">Anual</option>
                </select>
              </div>
              <div style={{ flex: 1 }}>
                <label className="rec-field-label">Parcelas (opcional)</label>
                <input type="number" min="1" placeholder="—" value={recParcelas}
                  onChange={e => setRecParcelas(e.target.value)} className="rec-field-input" />
              </div>
            </div>
            <div className="conta-form-row" style={{ marginTop: 8 }}>
              <div style={{ flex: 1 }}>
                <label className="rec-field-label">Dia de vencimento (opcional)</label>
                <input type="number" min="1" max="31" placeholder={`Padrão: dia ${recInicio ? new Date(recInicio + 'T12:00:00').getDate() : '—'}`}
                  value={recDiaVencimento} onChange={e => setRecDiaVencimento(e.target.value)} className="rec-field-input" />
              </div>
              <div style={{ flex: 2, display: 'flex', alignItems: 'center', gap: 6, paddingTop: 18 }}>
                <input id="rec-valor-variavel" type="checkbox" className="rec-field-checkbox" checked={recValorVariavel}
                  onChange={e => setRecValorVariavel(e.target.checked)} />
                <label htmlFor="rec-valor-variavel" style={{ fontSize: 13, color: 'var(--tx2)' }}>
                  Valor variável — previsto = média das últimas 3 ocorrências pagas
                </label>
              </div>
            </div>
          </div>
        )}

        <div className="conta-form-footer">
          <button className="btn-add-conta" onClick={handleAdicionar} disabled={saving || !podeAdicionar}>
            {saving ? 'Salvando...' : isRecorrente ? '＋ Adicionar Recorrente' : '＋ Adicionar'}
          </button>
        </div>
        {msg && <div style={{ marginTop: 8, fontSize: 13, fontWeight: 600, color: msgOk ? '#34c759' : '#ff6b6b' }}>{msg}</div>}
      </div>

      {sugestoesVisiveis.length > 0 && (
        <div className="contas-section">
          <h3>💡 Possíveis pagamentos encontrados ({sugestoesVisiveis.length})</h3>
          {sugestoesVisiveis.map(s => (
            <div key={s.contaProvisionadaId} className="conta-item" style={{ flexWrap: 'wrap', gap: 8 }}>
              <div style={{ flex: 1, minWidth: 220 }}>
                <strong>{s.descricao}</strong> — {fmtBRL(s.valor)}{s.dataVencimento ? ` (venc. ${fmtDate(s.dataVencimento)})` : ''}
                <div style={{ fontSize: 12, color: 'var(--tx3)' }}>
                  Lançamento no extrato: {s.lancamentoDescricao} — {fmtBRL(s.lancamentoValor)} em {fmtDate(s.lancamentoData)} · {s.score}% de confiança
                </div>
              </div>
              <div style={{ display: 'flex', gap: 6 }}>
                <button className="btn-confirm" disabled={vinculandoSugestaoId === s.contaProvisionadaId}
                  onClick={() => vincularSugestao(s)}>
                  {vinculandoSugestaoId === s.contaProvisionadaId ? 'Vinculando...' : '🔗 Vincular'}
                </button>
                <button className="btn-cancel" disabled={vinculandoSugestaoId === s.contaProvisionadaId}
                  onClick={() => ignorarSugestao(s)}>
                  Ignorar
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Item 2.5: totais pendentes no topo + filtro por período/conta — afeta as listas abaixo. */}
      <div className="contas-totais">
        <div className="contas-total-card receber">
          <span className="contas-total-label">A receber</span>
          <span className="contas-total-valor">{fmtBRL(totalPendenteReceber)}</span>
        </div>
        <div className="contas-total-card pagar">
          <span className="contas-total-label">A pagar</span>
          <span className="contas-total-valor">{fmtBRL(totalPendentePagar)}</span>
        </div>
      </div>

      <div className="contas-filtros">
        <div className="conta-field" style={{ flex: '0 1 160px' }}>
          <label className="conta-field-label" htmlFor="filtro-conta">Conta</label>
          <select id="filtro-conta" value={filtroContaId} onChange={e => setFiltroContaId(e.target.value)}>
            <option value="">Todas</option>
            {contasBancarias.filter(c => c.ativa).map(c => <option key={c.id} value={c.id}>{c.nome}</option>)}
          </select>
        </div>
        <div className="conta-field" style={{ flex: '0 1 150px' }}>
          <label className="conta-field-label" htmlFor="filtro-de">De</label>
          <input id="filtro-de" type="date" value={filtroDe} onChange={e => setFiltroDe(e.target.value)} />
        </div>
        <div className="conta-field" style={{ flex: '0 1 150px' }}>
          <label className="conta-field-label" htmlFor="filtro-ate">Até</label>
          <input id="filtro-ate" type="date" value={filtroAte} onChange={e => setFiltroAte(e.target.value)} />
        </div>
        {(filtroContaId || filtroDe || filtroAte) && (
          <button type="button" className="btn-cancel" style={{ alignSelf: 'flex-end' }}
            onClick={() => { setFiltroContaId(''); setFiltroDe(''); setFiltroAte('') }}>
            Limpar filtros
          </button>
        )}
      </div>

      <div className="contas-section">
        <h3>📥 A Receber ({pendentesReceber.length})</h3>
        {pendentesReceber.length === 0 && <p style={{ color: 'var(--tx3)', fontSize: 13 }}>Nenhuma pendente.</p>}
        {pendentesReceber.map(renderConta)}
      </div>

      <div className="contas-section">
        <h3>📤 A Pagar ({pendentesPagar.length})</h3>
        {pendentesPagar.length === 0 && <p style={{ color: 'var(--tx3)', fontSize: 13 }}>Nenhuma pendente.</p>}
        {pendentesPagar.map(renderConta)}
      </div>

      {recebidasList.length > 0 && (
        <div className="contas-section">
          <h3>✅ Já Recebidas</h3>
          {recebidasList.map(renderConta)}
        </div>
      )}

      {pagasList.length > 0 && (
        <div className="contas-section">
          <h3>✅ Já Pagas</h3>
          {pagasList.map(renderConta)}
        </div>
      )}

      <Modal
        open={showDuplicateModal}
        title="Conta semelhante já existe"
        onClose={() => {
          setShowDuplicateModal(false)
          setPendingDuplicate(null)
        }}
        footer={(
          <>
            <button className="btn-cancel" onClick={() => {
              setShowDuplicateModal(false)
              setPendingDuplicate(null)
            }}>Cancelar</button>
            <button className="btn-confirm" onClick={() => confirmarDuplicata(true)}>Vincular à existente</button>
            <button className="btn-confirm" onClick={() => confirmarDuplicata(false)}>Criar nova</button>
          </>
        )}
      >
        <p style={{ color: 'var(--tx3)', marginBottom: 8 }}>
          Encontramos uma conta parecida para a mesma descrição, valor e data{contaSelecionada ? ` em ${contaSelecionada.nome}` : ''}. Você pode vincular a entrada existente ou criar uma nova.
        </p>
      </Modal>

      <Modal
        open={modalBaixa}
        title={baixaView?.tipo === 'receber' ? '📥 Confirmar recebimento' : '📤 Confirmar pagamento'}
        onClose={() => { setModalBaixa(false); setBaixaView(null) }}
        footer={lancamentoDuplicado ? (
          <>
            <button className="btn-cancel" onClick={() => { setModalBaixa(false); setBaixaView(null) }}>Cancelar</button>
            <button className="btn-confirm" onClick={() => confirmarBaixa(true)} disabled={confirmandoBaixa}>Vincular a esse lançamento</button>
            <button className="btn-confirm" onClick={() => confirmarBaixa(false)} disabled={confirmandoBaixa}>Criar novo</button>
          </>
        ) : (
          <>
            <button className="btn-cancel" onClick={() => { setModalBaixa(false); setBaixaView(null) }}>Cancelar</button>
            <button className="btn-confirm" onClick={() => confirmarBaixa(false)} disabled={confirmandoBaixa}>
              {confirmandoBaixa ? 'Confirmando...' : 'Confirmar'}
            </button>
          </>
        )}
      >
        {baixaView && (
          <>
            <div className="conta-info" style={{ marginBottom: 12 }}>
              <div className="conta-desc">{baixaView.conta.descricao}</div>
              <div className="conta-meta">{fmtBRL(baixaView.conta.valor)} · {fmtDate(baixaView.registroData)}</div>
            </div>
            <div className="inp-group">
              <label>Conta bancária</label>
              <select value={baixaContaId} onChange={e => setBaixaContaId(e.target.value)}>
                {contasBancarias.filter(c => c.ativa).map(c => (
                  <option key={c.id} value={c.id}>{c.nome}</option>
                ))}
              </select>
            </div>
            <div className="inp-group">
              <label>{baixaView.tipo === 'receber' ? 'Data do recebimento' : 'Data do pagamento'}</label>
              <input type="date" value={baixaData} max={todayISO()} onChange={e => e.target.value && setBaixaData(e.target.value)} />
            </div>
            <div className="inp-group">
              <label>{baixaView.tipo === 'receber' ? 'Valor recebido (R$)' : 'Valor pago (R$)'}</label>
              <div className="val-input-wrap">
                <span className="val-prefix">R$</span>
                <input
                  type="text" inputMode="decimal"
                  value={baixaValorDisplay}
                  onChange={e => {
                    const raw = e.target.value.replace(/[^\d,]/g, '')
                    setBaixaValorDisplay(raw)
                    setBaixaValor(parseBRL(raw))
                  }}
                  onBlur={() => setBaixaValorDisplay(fmtNum(baixaValor))}
                />
              </div>
              {Math.abs(baixaValor - baixaView.conta.valor) >= 0.01 && (
                <span style={{ fontSize: 12, color: 'var(--tx3)' }}>
                  Valor do título: {fmtBRL(baixaView.conta.valor)} — diferença de {fmtBRL(baixaValor - baixaView.conta.valor)} (juros/desconto).
                </span>
              )}
            </div>
            {lancamentoDuplicado && (
              <p style={{ color: 'var(--warning)', marginTop: 8, fontSize: 13 }}>
                Já existe um lançamento de {fmtBRL(baixaView.conta.valor)} nesta conta nesta data
                ("{lancamentoDuplicado.descricao}"). Vincular a baixa a esse lançamento ou criar um novo?
              </p>
            )}
          </>
        )}
      </Modal>

      <Modal
        open={!!estornoView}
        title="↩ Estornar baixa"
        onClose={() => setEstornoView(null)}
        footer={
          <>
            <button className="btn-cancel" onClick={() => setEstornoView(null)} disabled={estornando}>Cancelar</button>
            <button className="btn-confirm" onClick={confirmarEstorno} disabled={estornando}>
              {estornando ? 'Estornando...' : 'Estornar'}
            </button>
          </>
        }
      >
        {estornoView && (
          <>
            <div className="conta-info" style={{ marginBottom: 12 }}>
              <div className="conta-desc">{estornoView.conta.descricao}</div>
              <div className="conta-meta">{fmtBRL(estornoView.conta.valor)} · {estornoView.conta.dataBaixa ? `baixa em ${fmtDate(estornoView.conta.dataBaixa)}` : ''}</div>
            </div>
            <p style={{ color: 'var(--warning)', fontSize: 13, marginBottom: estornoParaEditar ? 8 : 0 }}>
              {impactoEstorno(estornoView)}
            </p>
            {estornoParaEditar && (
              <p style={{ color: 'var(--tx3)', fontSize: 13 }}>
                Depois de estornada, a conta volta pra lista de pendentes e o formulário de edição abre em seguida.
              </p>
            )}
          </>
        )}
      </Modal>

      <Modal
        open={!!editView}
        title="✏️ Editar conta"
        onClose={() => setEditView(null)}
        footer={
          <>
            <button className="btn-cancel" onClick={() => setEditView(null)} disabled={salvandoEdit}>Cancelar</button>
            <button className="btn-confirm" onClick={confirmarEdicaoConta} disabled={salvandoEdit || !editDesc.trim() || !editValor}>
              {salvandoEdit ? 'Salvando...' : 'Salvar'}
            </button>
          </>
        }
      >
        {editView && (
          <>
            <div className="inp-group">
              <label>Descrição</label>
              <input value={editDesc} onChange={e => setEditDesc(e.target.value)} />
            </div>
            <div className="inp-group" style={{ marginTop: 12 }}>
              <label>Valor</label>
              <div className="val-input-wrap">
                <span className="val-prefix">R$</span>
                <input
                  type="text" inputMode="decimal" placeholder="0,00"
                  value={editValorDisplay}
                  onChange={e => {
                    const raw = e.target.value.replace(/[^\d,]/g, '')
                    setEditValorDisplay(raw)
                    setEditValor(parseBRL(raw))
                  }}
                  onBlur={() => setEditValorDisplay(editValor ? fmtNum(editValor) : '')}
                />
              </div>
            </div>
            <div className="inp-group" style={{ marginTop: 12 }}>
              <label>Vencimento</label>
              <input type="date" value={editVenc} onChange={e => setEditVenc(e.target.value)} />
            </div>
            <div className="inp-group" style={{ marginTop: 12 }}>
              <label>Conta bancária</label>
              <select value={editContaId} onChange={e => setEditContaId(e.target.value)}>
                {contasBancarias.filter(c => c.ativa).map(c => (
                  <option key={c.id} value={c.id}>{c.nome}</option>
                ))}
              </select>
            </div>
          </>
        )}
      </Modal>

      <Modal
        open={!!excluirView}
        title="Excluir conta"
        onClose={() => setExcluirView(null)}
        footer={
          <>
            <button className="btn-cancel" onClick={() => setExcluirView(null)} disabled={excluindo}>Cancelar</button>
            <button className="btn-confirm" onClick={confirmarExclusaoConta} disabled={excluindo}>
              {excluindo ? 'Excluindo...' : 'Excluir'}
            </button>
          </>
        }
      >
        {excluirView && (
          <p style={{ color: 'var(--tx3)' }}>
            Excluir "{excluirView.conta.descricao}" ({fmtBRL(excluirView.conta.valor)})? Essa conta ainda não foi
            {excluirView.tipo === 'receber' ? ' recebida' : ' paga'} — excluir não afeta o saldo.
          </p>
        )}
      </Modal>

      {recorrentes.length > 0 && (
        <div className="contas-section">
          <h3>🔁 Recorrentes Ativas ({recorrentes.length})</h3>
          {recorrentes.map(r => (
            <div key={r.id} className="conta-item">
              <div className="conta-info">
                <div className="conta-desc">{r.descricao}</div>
                <div className="conta-meta">
                  {r.tipo} · {fmtBRL(r.valor)} · {r.periodicidade} · desde {fmtDate(r.dataInicio)}
                  {r.dataFim ? ` até ${fmtDate(r.dataFim)}` : ''}
                  {r.quantidadeParcelas ? ` · ${r.quantidadeParcelas}x` : ''}
                  {r.contaBancariaId ? ` · ${contasBancarias.find(c => c.id === r.contaBancariaId)?.nome ?? 'Conta'}` : ''}
                </div>
              </div>
              <div className="conta-item-acoes">
                <button className="cb-btn-editar" onClick={() => abrirEdicaoRecorrencia(r)}>Editar</button>
                <button className="cb-btn-inativar" onClick={() => abrirExclusaoRecorrencia(r)}>Excluir</button>
              </div>
            </div>
          ))}
        </div>
      )}

      <Modal
        open={!!editRec}
        title="✏️ Editar recorrência"
        onClose={() => setEditRec(null)}
        footer={
          <>
            <button className="btn-cancel" onClick={() => setEditRec(null)} disabled={salvandoEditRec}>Cancelar</button>
            <button className="btn-confirm" onClick={confirmarEdicaoRecorrencia} disabled={salvandoEditRec || !editRecAlcance || !editRecValor}>
              {salvandoEditRec ? 'Salvando...' : 'Salvar'}
            </button>
          </>
        }
      >
        {editRec && (
          <>
            <div className="conta-info" style={{ marginBottom: 12 }}>
              <div className="conta-desc">{editRec.descricao}</div>
            </div>
            <div className="inp-group">
              <label>Valor</label>
              <div className="val-input-wrap">
                <span className="val-prefix">R$</span>
                <input
                  type="text" inputMode="decimal" placeholder="0,00"
                  value={editRecValorDisplay}
                  onChange={e => {
                    const raw = e.target.value.replace(/[^\d,]/g, '')
                    setEditRecValorDisplay(raw)
                    setEditRecValor(parseBRL(raw))
                  }}
                  onBlur={() => setEditRecValorDisplay(editRecValor ? fmtNum(editRecValor) : '')}
                />
              </div>
            </div>
            <div className="inp-group" style={{ marginTop: 12 }}>
              <label>Periodicidade</label>
              <select value={editRecPeriodicidade} onChange={e => setEditRecPeriodicidade(e.target.value)}>
                <option value="Mensal">Mensal</option>
                <option value="Semanal">Semanal</option>
                <option value="Quinzenal">Quinzenal</option>
                <option value="Trimestral">Trimestral</option>
                <option value="Semestral">Semestral</option>
                <option value="Anual">Anual</option>
              </select>
            </div>
            <div className="inp-group" style={{ marginTop: 12 }}>
              <label>Início</label>
              <input type="date" value={editRecInicio} onChange={e => setEditRecInicio(e.target.value)} />
            </div>
            <div className="inp-group" style={{ marginTop: 12 }}>
              <label>Fim (opcional)</label>
              <input type="date" value={editRecFim} onChange={e => setEditRecFim(e.target.value)} />
            </div>
            <div className="inp-group" style={{ marginTop: 12 }}>
              <label>Dia de vencimento (opcional)</label>
              <input type="number" min="1" max="31" value={editRecDiaVencimento}
                onChange={e => setEditRecDiaVencimento(e.target.value)} />
            </div>
            <div className="inp-group" style={{ marginTop: 12, display: 'flex', alignItems: 'center', gap: 6 }}>
              <input id="edit-rec-valor-variavel" type="checkbox" className="rec-field-checkbox" checked={editRecValorVariavel}
                onChange={e => setEditRecValorVariavel(e.target.checked)} />
              <label htmlFor="edit-rec-valor-variavel" style={{ fontSize: 13, margin: 0 }}>
                Valor variável — previsto = média das últimas 3 ocorrências pagas
              </label>
            </div>
            <div className="inp-group" style={{ marginTop: 12 }}>
              <label>Conta bancária</label>
              <select value={editRecContaId} onChange={e => setEditRecContaId(e.target.value)}>
                <option value="">Sem conta definida (escolhe na baixa)</option>
                {contasBancarias.filter(c => c.ativa).map(c => (
                  <option key={c.id} value={c.id}>{c.nome}</option>
                ))}
              </select>
            </div>

            <div className="inp-group" style={{ marginTop: 16, paddingTop: 12, borderTop: '1px solid var(--bd)' }}>
              <label>Aplicar essas mudanças a:</label>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 8, marginTop: 6 }}>
                <label style={{ display: 'flex', alignItems: 'flex-start', gap: 8, fontSize: 13, fontWeight: 400, cursor: 'pointer' }}>
                  <input type="radio" name="editRecAlcance" checked={editRecAlcance === 'futuras'}
                    onChange={() => setEditRecAlcance('futuras')} style={{ marginTop: 2 }} />
                  <span>Só as próximas ocorrências — as pendentes já geradas mantêm os valores antigos.</span>
                </label>
                <label style={{ display: 'flex', alignItems: 'flex-start', gap: 8, fontSize: 13, fontWeight: 400, cursor: 'pointer' }}>
                  <input type="radio" name="editRecAlcance" checked={editRecAlcance === 'todas'}
                    onChange={() => setEditRecAlcance('todas')} style={{ marginTop: 2 }} />
                  <span>Também as pendentes já geradas — atualiza valor/conta bancária nelas. As que já foram pagas nunca mudam.</span>
                </label>
              </div>
            </div>
          </>
        )}
      </Modal>

      <Modal
        open={!!excluirRec}
        title="Excluir recorrência"
        onClose={() => setExcluirRec(null)}
        footer={
          <>
            <button className="btn-cancel" onClick={() => setExcluirRec(null)} disabled={excluindoRec}>Cancelar</button>
            <button className="btn-confirm" onClick={confirmarExclusaoRecorrencia} disabled={excluindoRec || !excluirRecPendentes}>
              {excluindoRec ? 'Excluindo...' : 'Excluir'}
            </button>
          </>
        }
      >
        {excluirRec && (
          <>
            <p style={{ color: 'var(--tx3)', marginBottom: 12 }}>
              Excluir a recorrência "{excluirRec.descricao}"? Ela para de gerar novas ocorrências. Isso nunca afeta
              ocorrências já pagas — elas continuam no histórico normalmente.
            </p>
            <div className="inp-group">
              <label>O que fazer com as ocorrências pendentes já geradas por ela?</label>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 8, marginTop: 6 }}>
                <label style={{ display: 'flex', alignItems: 'flex-start', gap: 8, fontSize: 13, fontWeight: 400, cursor: 'pointer' }}>
                  <input type="radio" name="excluirRecPendentes" checked={excluirRecPendentes === 'manter'}
                    onChange={() => setExcluirRecPendentes('manter')} style={{ marginTop: 2 }} />
                  <span>Manter — viram contas avulsas, continuam pendentes normalmente.</span>
                </label>
                <label style={{ display: 'flex', alignItems: 'flex-start', gap: 8, fontSize: 13, fontWeight: 400, cursor: 'pointer' }}>
                  <input type="radio" name="excluirRecPendentes" checked={excluirRecPendentes === 'remover'}
                    onChange={() => setExcluirRecPendentes('remover')} style={{ marginTop: 2 }} />
                  <span>Remover — some da lista de pendentes (as já pagas nunca são removidas).</span>
                </label>
              </div>
            </div>
          </>
        )}
      </Modal>
    </>
  )
}
