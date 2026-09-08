import { useState, useMemo, useEffect } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { useRegistros } from '../../hooks/useRegistros'
import { fmtBRL, fmtDate, todayISO, addDays } from '../../utils/format'
import {
  listarContasRecorrentes, criarContaRecorrente, atualizarContaRecorrente, desativarContaRecorrente,
} from '../../api/contasRecorrentes'
import { listarContasBancarias } from '../../api/contasBancarias'
import Modal from '../../components/shared/Modal'
import type { ContaProvisionada, ContaRecorrente, ContaBancaria } from '../../types'
import './ClientContas.css'
import './ClientContasBancarias.css'

interface Props { clienteIdOverride?: string }

interface ContaView {
  registroData: string
  // Conta bancária do RegistroDiario onde esta ContaProvisionada realmente vive — necessário pra
  // localizar o registro sem ambiguidade quando existe mais de um registro na mesma data (uma por
  // conta bancária diferente).
  registroContaId?: string
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
  // '' força o usuário a escolher — nunca decide silenciosamente o que acontece com as já geradas.
  const [editRecAlcance, setEditRecAlcance] = useState<'' | 'futuras' | 'todas'>('')
  const [salvandoEditRec, setSalvandoEditRec] = useState(false)

  // ── Excluir recorrência ───────────────────────────────────────────────────────────────────
  const [excluirRec, setExcluirRec] = useState<ContaRecorrente | null>(null)
  const [excluirRecPendentes, setExcluirRecPendentes] = useState<'' | 'manter' | 'remover'>('')
  const [excluindoRec, setExcluindoRec] = useState(false)

  useEffect(() => {
    if (!clienteId) return
    listarContasRecorrentes(clienteId).then(setRecorrentes).catch(console.error)
    listarContasBancarias(clienteId).then(setContasBancarias).catch(console.error)
  }, [clienteId])

  useEffect(() => {
    if (!contasBancarias.length) return
    if (!contaSelecionadaId) {
      const caixa = contasBancarias.find(c => c.tipo === 'Caixa' || c.nome.toLowerCase() === 'caixa') ?? contasBancarias[0]
      setContaSelecionadaId(caixa.id)
    }
  }, [contasBancarias, contaSelecionadaId])

  function resetForm() {
    setDesc(''); setValorDisplay(''); setValor(0); setVenc(todayISO())
    setRecInicio(todayISO()); setRecFim(''); setRecPeriodicidade('Mensal'); setRecParcelas('')
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
        const nova = await criarContaRecorrente({
          clienteId, descricao: desc, valor,
          tipo: tipo === 'receber' ? 'Receber' : 'Pagar',
          dataInicio: recInicio, dataFim: recFim || undefined,
          periodicidade: recPeriodicidade,
          quantidadeParcelas: recParcelas ? Number(recParcelas) : undefined,
          contaBancariaId: contaSelecionadaId || undefined,
        })
        setRecorrentes(prev => [...prev, nova])
        setMsg('Conta recorrente adicionada!')
        setMsgOk(true)
      } else {
        if (!contaSelecionadaId) { setMsg('Cadastre uma conta bancária em Configurações antes de continuar.'); setMsgOk(false); return }
        const hoje = todayISO()
        // Precisa ser o registro da MESMA conta escolhida — senão a mesclagem pega
        // entradas/saídas/pendências de uma conta diferente ou fica sem nenhuma base.
        const reg = registros.find(r => r.data === hoje && r.contaBancariaId === contaSelecionadaId)
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
        await salvar({
          clienteId, contaBancariaId: contaSelecionadaId, data: hoje,
          saldoInicio: reg?.saldoInicio ?? 0,
          entradas: reg?.entradas ?? [],
          saidas: reg?.saidas ?? [],
          contasAReceber: tipo === 'receber' ? [...(reg?.contasAReceber ?? []), novaConta] : (reg?.contasAReceber ?? []),
          contasAPagar: tipo === 'pagar' ? [...(reg?.contasAPagar ?? []), novaConta] : (reg?.contasAPagar ?? []),
          saldoConfirmado: reg?.saldoConfirmado ?? 0,
        })
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
      reg.contasAReceber.forEach((c, i) => acc.push({ registroData: reg.data, registroContaId: reg.contaBancariaId, tipo: 'receber', index: i, conta: c }))
      reg.contasAPagar.forEach((c, i) => acc.push({ registroData: reg.data, registroContaId: reg.contaBancariaId, tipo: 'pagar', index: i, conta: c }))
    }
    return acc.sort((a, b) => {
      const da = a.conta.dataVencimento ?? a.registroData
      const db = b.conta.dataVencimento ?? b.registroData
      return da.localeCompare(db)
    })
  }, [registros])

  const pendentesReceber = todasContas.filter(c => c.tipo === 'receber' && !c.conta.pago)
  const recebidasList = todasContas.filter(c => c.tipo === 'receber' && c.conta.pago)
  const pendentesPagar = todasContas.filter(c => c.tipo === 'pagar' && !c.conta.pago)
  const pagasList = todasContas.filter(c => c.tipo === 'pagar' && c.conta.pago)
  const contaSelecionada = contasBancarias.find(c => c.id === contaSelecionadaId)

  function origemRegistro(view: ContaView) {
    return registros.find(r => r.data === view.registroData && r.contaBancariaId === view.registroContaId)
      ?? registros.find(r => r.data === view.registroData)
  }

  async function persistirListas(view: ContaView, transformar: (contas: ContaProvisionada[]) => ContaProvisionada[]) {
    if (!clienteId) return
    const reg = origemRegistro(view)
    if (!reg) return
    await salvar({
      clienteId, contaBancariaId: reg.contaBancariaId, data: reg.data, saldoInicio: reg.saldoInicio,
      entradas: reg.entradas, saidas: reg.saidas,
      contasAReceber: view.tipo === 'receber' ? transformar(reg.contasAReceber) : reg.contasAReceber,
      contasAPagar: view.tipo === 'pagar' ? transformar(reg.contasAPagar) : reg.contasAPagar,
      saldoConfirmado: reg.saldoConfirmado,
    })
  }

  async function desfazerBaixa(view: ContaView) {
    await persistirListas(view, contas => contas.map((c, i) =>
      i === view.index ? { ...c, pago: false, dataBaixa: undefined, lancamentoVinculadoId: undefined } : c))
  }

  function togglePago(view: ContaView) {
    setBaixaView(view)
    setBaixaContaId(view.conta.contaBancariaId || contaSelecionadaId)
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
      await persistirListas(baixaView, contas => contas.map((c, i) =>
        i === baixaView.index ? { ...c, pago: true, contaBancariaId: baixaContaId, lancamentoVinculadoId } : c))
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
    if (!editView || !editDesc.trim() || !editValor) return
    setSalvandoEdit(true)
    try {
      await persistirListas(editView, contas => contas.map((c, i) => i === editView.index
        ? { ...c, descricao: editDesc.trim(), valor: editValor, dataVencimento: editVenc || undefined, contaBancariaId: editContaId || undefined }
        : c))
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
    if (!excluirView) return
    setExcluindo(true)
    try {
      await persistirListas(excluirView, contas => contas.filter((_, i) => i !== excluirView.index))
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
    const hoje = todayISO()
    const contaAlvo = pendingDuplicate.conta.contaBancariaId || contaSelecionadaId
    const reg = registros.find(r => r.data === hoje && r.contaBancariaId === contaAlvo)

    try {
      if (!linkToExisting) {
        await salvar({
          clienteId, contaBancariaId: contaAlvo, data: hoje,
          saldoInicio: reg?.saldoInicio ?? 0,
          entradas: reg?.entradas ?? [],
          saidas: reg?.saidas ?? [],
          contasAReceber: pendingDuplicate.tipo === 'receber' ? [...(reg?.contasAReceber ?? []), pendingDuplicate.conta] : (reg?.contasAReceber ?? []),
          contasAPagar: pendingDuplicate.tipo === 'pagar' ? [...(reg?.contasAPagar ?? []), pendingDuplicate.conta] : (reg?.contasAPagar ?? []),
          saldoConfirmado: reg?.saldoConfirmado ?? 0,
        })
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

    return (
    <div key={`${view.registroData}-${view.tipo}-${view.index}`} className={`conta-item ${view.conta.pago ? 'pago' : ''}`}>
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
        </div>
      </div>
      <div className={`conta-valor ${view.tipo}`}>{fmtBRL(view.conta.valor)}</div>
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

  const podeAdicionar = !!desc && valor > 0 && (!isRecorrente || !!recInicio) && (isRecorrente || !!contaSelecionadaId)

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
                {isRecorrente ? `Conta sugerida na baixa de cada ocorrência: ${contaSelecionada.nome}.` : `O lançamento será vinculado a ${contaSelecionada.nome}.`}
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
          </div>
        )}

        <div className="conta-form-footer">
          <button className="btn-add-conta" onClick={handleAdicionar} disabled={saving || !podeAdicionar}>
            {saving ? 'Salvando...' : isRecorrente ? '＋ Adicionar Recorrente' : '＋ Adicionar'}
          </button>
        </div>
        {msg && <div style={{ marginTop: 8, fontSize: 13, fontWeight: 600, color: msgOk ? '#34c759' : '#ff6b6b' }}>{msg}</div>}
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
