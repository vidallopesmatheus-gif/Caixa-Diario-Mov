import { useState, useMemo, useEffect } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { useRegistros } from '../../hooks/useRegistros'
import { fmtBRL, fmtDate, todayISO } from '../../utils/format'
import { listarContasRecorrentes, criarContaRecorrente, desativarContaRecorrente } from '../../api/contasRecorrentes'
import { listarContasBancarias } from '../../api/contasBancarias'
import Modal from '../../components/shared/Modal'
import type { ContaProvisionada, ContaRecorrente, ContaBancaria } from '../../types'
import './ClientContas.css'

interface Props { clienteIdOverride?: string }

interface ContaView {
  registroData: string
  tipo: 'receber' | 'pagar'
  index: number
  conta: ContaProvisionada
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
  const { registros, salvar, loading } = useRegistros(clienteId)

  // Formulário unificado
  const [tipo, setTipo] = useState<'receber' | 'pagar'>('receber')
  const [desc, setDesc] = useState('')
  const [valorDisplay, setValorDisplay] = useState('')
  const [valor, setValor] = useState(0)
  const [venc, setVenc] = useState('')
  const [isRecorrente, setIsRecorrente] = useState(false)
  const [recInicio, setRecInicio] = useState('')
  const [recFim, setRecFim] = useState('')
  const [recPeriodicidade, setRecPeriodicidade] = useState('Mensal')
  const [recParcelas, setRecParcelas] = useState('')
  const [saving, setSaving] = useState(false)
  const [msg, setMsg] = useState('')

  const [recorrentes, setRecorrentes] = useState<ContaRecorrente[]>([])
  const [contasBancarias, setContasBancarias] = useState<ContaBancaria[]>([])
  const [contaSelecionadaId, setContaSelecionadaId] = useState('')
  const [showDuplicateModal, setShowDuplicateModal] = useState(false)
  const [pendingDuplicate, setPendingDuplicate] = useState<{
    tipo: 'receber' | 'pagar'
    conta: ContaProvisionada
    registroData: string
  } | null>(null)

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
    setDesc(''); setValorDisplay(''); setValor(0); setVenc('')
    setRecInicio(''); setRecFim(''); setRecPeriodicidade('Mensal'); setRecParcelas('')
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
        if (!recInicio) { setMsg('Informe a data de início.'); return }
        const nova = await criarContaRecorrente({
          clienteId, descricao: desc, valor,
          tipo: tipo === 'receber' ? 'Receber' : 'Pagar',
          dataInicio: recInicio, dataFim: recFim || undefined,
          periodicidade: recPeriodicidade,
          quantidadeParcelas: recParcelas ? Number(recParcelas) : undefined,
        })
        setRecorrentes(prev => [...prev, nova])
        setMsg('Conta recorrente adicionada!')
      } else {
        const hoje = todayISO()
        const reg = registros.find(r => r.data === hoje)
        const novaConta: ContaProvisionada = {
          descricao: desc,
          valor,
          dataVencimento: venc || undefined,
          pago: false,
          contaBancariaId: contaSelecionadaId || undefined,
        }
        const duplicata = encontrarDuplicata(novaConta, hoje)
        if (duplicata) {
          setPendingDuplicate({ tipo, conta: novaConta, registroData: hoje })
          setShowDuplicateModal(true)
          return
        }
        await salvar({
          clienteId, data: hoje,
          saldoInicio: reg?.saldoInicio ?? 0,
          entradas: reg?.entradas ?? [],
          saidas: reg?.saidas ?? [],
          contasAReceber: tipo === 'receber' ? [...(reg?.contasAReceber ?? []), novaConta] : (reg?.contasAReceber ?? []),
          contasAPagar: tipo === 'pagar' ? [...(reg?.contasAPagar ?? []), novaConta] : (reg?.contasAPagar ?? []),
          saldoConfirmado: reg?.saldoConfirmado ?? 0,
        })
        setMsg('Conta adicionada!')
      }
      resetForm()
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : String(e))
    } finally {
      setSaving(false)
    }
  }

  async function handleDesativar(id: string) {
    if (!clienteId) return
    await desativarContaRecorrente(clienteId, id)
    setRecorrentes(prev => prev.filter(r => r.id !== id))
  }

  const todasContas = useMemo<ContaView[]>(() => {
    const acc: ContaView[] = []
    for (const reg of registros) {
      reg.contasAReceber.forEach((c, i) => acc.push({ registroData: reg.data, tipo: 'receber', index: i, conta: c }))
      reg.contasAPagar.forEach((c, i) => acc.push({ registroData: reg.data, tipo: 'pagar', index: i, conta: c }))
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

  async function togglePago(view: ContaView) {
    if (!clienteId) return
    const reg = registros.find(r => r.data === view.registroData)
    if (!reg) return
    await salvar({
      clienteId, data: reg.data, saldoInicio: reg.saldoInicio,
      entradas: reg.entradas, saidas: reg.saidas,
      contasAReceber: reg.contasAReceber.map((c, i) => view.tipo === 'receber' && i === view.index ? { ...c, pago: !c.pago } : c),
      contasAPagar: reg.contasAPagar.map((c, i) => view.tipo === 'pagar' && i === view.index ? { ...c, pago: !c.pago } : c),
      saldoConfirmado: reg.saldoConfirmado,
    })
  }

  async function confirmarDuplicata(linkToExisting: boolean) {
    if (!pendingDuplicate || !clienteId) return
    const hoje = todayISO()
    const reg = registros.find(r => r.data === hoje)
    if (!reg) return

    if (!linkToExisting) {
      await salvar({
        clienteId, data: hoje,
        saldoInicio: reg.saldoInicio,
        entradas: reg.entradas,
        saidas: reg.saidas,
        contasAReceber: pendingDuplicate.tipo === 'receber' ? [...(reg.contasAReceber ?? []), pendingDuplicate.conta] : (reg.contasAReceber ?? []),
        contasAPagar: pendingDuplicate.tipo === 'pagar' ? [...(reg.contasAPagar ?? []), pendingDuplicate.conta] : (reg.contasAPagar ?? []),
        saldoConfirmado: reg.saldoConfirmado,
      })
      setMsg('Nova conta criada.')
    } else {
      setMsg('Conta semelhante já existente; nenhuma nova entrada foi criada.')
    }

    setShowDuplicateModal(false)
    setPendingDuplicate(null)
    resetForm()
  }

  if (loading) return <p style={{ color: 'var(--tx3)' }}>Carregando...</p>

  const renderConta = (view: ContaView) => {
    const contaNome = contasBancarias.find(c => c.id === view.conta.contaBancariaId)?.nome ?? 'Conta padrão'

    return (
    <div key={`${view.registroData}-${view.tipo}-${view.index}`} className={`conta-item ${view.conta.pago ? 'pago' : ''}`}>
      <button
        onClick={() => togglePago(view)}
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
    </div>
    )
  }

  const podeAdicionar = !!desc && valor > 0 && (!isRecorrente || !!recInicio)

  return (
    <>
      {/* Formulário unificado */}
      <div className="add-conta-form">
        <h4>＋ Nova Conta</h4>

        <div className="conta-form-row">
          <select value={tipo} onChange={e => setTipo(e.target.value as 'receber' | 'pagar')}
            style={{ maxWidth: 140 }}>
            <option value="receber">A Receber</option>
            <option value="pagar">A Pagar</option>
          </select>
          <input placeholder="Descrição" value={desc} onChange={e => setDesc(e.target.value)} style={{ flex: 2 }} />
          {!isRecorrente && (
            <div style={{ minWidth: 220, flex: 1.1 }}>
              <label className="conta-field-label">Conta bancária</label>
              <select value={contaSelecionadaId} onChange={e => setContaSelecionadaId(e.target.value)} style={{ width: '100%', maxWidth: 220 }}>
                {contasBancarias.filter(c => c.ativa).map(c => (
                  <option key={c.id} value={c.id}>{c.nome}</option>
                ))}
              </select>
              {contaSelecionada && (
                <div className="conta-field-hint">O lançamento será vinculado a {contaSelecionada.nome}.</div>
              )}
            </div>
          )}
          <div className="val-input-wrap" style={{ flex: 1, minWidth: 120 }}>
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
          {!isRecorrente && (
            <input type="date" value={venc} onChange={e => setVenc(e.target.value)}
              style={{ flex: 1, minWidth: 140, padding: '8px 12px', background: 'var(--bg)', border: '1px solid var(--bd)', borderRadius: 8, color: 'var(--tx1)', fontSize: 14 }} />
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

        <button className="btn-add-conta" onClick={handleAdicionar} disabled={saving || !podeAdicionar}
          style={{ marginTop: 12 }}>
          {saving ? 'Salvando...' : isRecorrente ? '＋ Adicionar Recorrente' : '＋ Adicionar'}
        </button>
        {msg && <div style={{ marginTop: 8, fontSize: 13, color: '#34c759' }}>{msg}</div>}
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
                </div>
              </div>
              <button style={{ fontSize: 12, padding: '4px 10px', background: '#ff3b30', border: 'none', borderRadius: 6, color: '#fff', cursor: 'pointer' }}
                onClick={() => handleDesativar(r.id)}>
                Desativar
              </button>
            </div>
          ))}
        </div>
      )}
    </>
  )
}
