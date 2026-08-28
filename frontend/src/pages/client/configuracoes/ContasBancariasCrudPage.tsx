import { useState, useEffect } from 'react'
import { useAuth } from '../../../contexts/AuthContext'
import {
  listarContasBancarias,
  criarContaBancaria,
  atualizarContaBancaria,
  inativarContaBancaria,
} from '../../../api/contasBancarias'
import { fmtBRL } from '../../../utils/format'
import type { ContaBancaria } from '../../../types'
import '../ClientContasBancarias.css'

interface Props { clienteIdOverride?: string }

const TIPOS = ['Caixa', 'ContaCorrente', 'Investimento'] as const
const TIPO_LABEL: Record<string, string> = {
  Caixa: '💵 Caixa',
  ContaCorrente: '🏦 Conta Corrente',
  Investimento: '📈 Investimento',
}

function fmtNum(n: number) {
  if (!n) return ''
  return n.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}
function parseBRL(s: string): number {
  return parseFloat(s.replace(/\./g, '').replace(',', '.')) || 0
}

/** Configurações → Contas Bancárias: cadastro (criar/editar/inativar). Operação fica na aba Banco. */
export default function ContasBancariasCrudPage({ clienteIdOverride }: Props) {
  const { user } = useAuth()
  const clienteId = clienteIdOverride ?? user?.usuarioId ?? null

  const [contas, setContas] = useState<ContaBancaria[]>([])
  const [loading, setLoading] = useState(true)
  const [msg, setMsg] = useState('')
  const [msgOk, setMsgOk] = useState(true)

  const [novoNome, setNovoNome] = useState('')
  const [novoTipo, setNovoTipo] = useState<string>('ContaCorrente')
  const [novoSaldoDisplay, setNovoSaldoDisplay] = useState('')
  const [novoSaldo, setNovoSaldo] = useState(0)
  const [criando, setCriando] = useState(false)

  const [editId, setEditId] = useState<string | null>(null)
  const [editNome, setEditNome] = useState('')
  const [editTipo, setEditTipo] = useState('')
  const [editSaldoDisplay, setEditSaldoDisplay] = useState('')
  const [editSaldo, setEditSaldo] = useState(0)
  const [editAtiva, setEditAtiva] = useState(true)
  const [salvandoEdit, setSalvandoEdit] = useState(false)

  useEffect(() => {
    if (!clienteId) return
    setLoading(true)
    listarContasBancarias(clienteId)
      .then(setContas)
      .catch(() => showMsg('Erro ao carregar contas.', false))
      .finally(() => setLoading(false))
  }, [clienteId])

  function showMsg(texto: string, ok = true) {
    setMsgOk(ok)
    setMsg(texto)
    setTimeout(() => setMsg(''), 3000)
  }

  async function handleCriar() {
    if (!clienteId || !novoNome.trim()) return
    setCriando(true)
    try {
      const nova = await criarContaBancaria({
        clienteId,
        nome: novoNome.trim(),
        tipo: novoTipo,
        saldoInicial: novoSaldo,
      })
      setContas(prev => [...prev, nova])
      setNovoNome('')
      setNovoTipo('ContaCorrente')
      setNovoSaldoDisplay('')
      setNovoSaldo(0)
      showMsg('Conta criada com sucesso!')
    } catch (e: unknown) {
      showMsg(e instanceof Error ? e.message : 'Erro ao criar conta.', false)
    } finally {
      setCriando(false)
    }
  }

  function iniciarEdicao(c: ContaBancaria) {
    setEditId(c.id)
    setEditNome(c.nome)
    setEditTipo(c.tipo)
    setEditSaldo(c.saldoInicial)
    setEditSaldoDisplay(c.saldoInicial ? fmtNum(c.saldoInicial) : '')
    setEditAtiva(c.ativa)
  }

  async function handleSalvarEdit() {
    if (!editId) return
    setSalvandoEdit(true)
    try {
      const atualizada = await atualizarContaBancaria(editId, {
        nome: editNome.trim(),
        tipo: editTipo,
        saldoInicial: editSaldo,
        ativa: editAtiva,
      })
      setContas(prev => prev.map(c => c.id === editId ? atualizada : c))
      setEditId(null)
      showMsg('Conta atualizada!')
    } catch (e: unknown) {
      showMsg(e instanceof Error ? e.message : 'Erro ao atualizar.', false)
    } finally {
      setSalvandoEdit(false)
    }
  }

  async function handleInativar(id: string) {
    if (!confirm('Inativar esta conta? Os registros vinculados serão preservados.')) return
    try {
      await inativarContaBancaria(id)
      setContas(prev => prev.map(c => c.id === id ? { ...c, ativa: false } : c))
      showMsg('Conta inativada.')
    } catch (e: unknown) {
      showMsg(e instanceof Error ? e.message : 'Erro ao inativar.', false)
    }
  }

  const ativas = contas.filter(c => c.ativa)
  const inativas = contas.filter(c => !c.ativa)

  if (loading) return <p style={{ color: 'var(--tx3)' }}>Carregando...</p>

  return (
    <>
      <div className="add-conta-form">
        <h4>＋ Nova Conta Bancária</h4>
        <div className="conta-form-row">
          <input
            placeholder="Nome da conta (ex: Nubank, Bradesco...)"
            value={novoNome}
            onChange={e => setNovoNome(e.target.value)}
            style={{ flex: 3 }}
          />
          <select value={novoTipo} onChange={e => setNovoTipo(e.target.value)} style={{ flex: 1, minWidth: 160 }}>
            {TIPOS.map(t => <option key={t} value={t}>{TIPO_LABEL[t]}</option>)}
          </select>
          <div className="val-input-wrap" style={{ flex: 1, minWidth: 130 }}>
            <span className="val-prefix">R$</span>
            <input
              type="text" inputMode="decimal" placeholder="0,00"
              value={novoSaldoDisplay}
              onChange={e => {
                const raw = e.target.value.replace(/[^\d,]/g, '')
                setNovoSaldoDisplay(raw)
                setNovoSaldo(parseBRL(raw))
              }}
              onBlur={() => setNovoSaldoDisplay(novoSaldo ? fmtNum(novoSaldo) : '')}
            />
          </div>
          <button
            className="btn-add-conta"
            onClick={handleCriar}
            disabled={criando || !novoNome.trim()}>
            {criando ? 'Criando...' : '＋ Criar'}
          </button>
        </div>
        {msg && (
          <div style={{ marginTop: 8, fontSize: 13, fontWeight: 600, color: msgOk ? '#34c759' : '#ff6b6b' }}>
            {msg}
          </div>
        )}
      </div>

      <div className="contas-section">
        <h3>🏦 Contas Ativas ({ativas.length})</h3>
        {ativas.length === 0 && <p style={{ color: 'var(--tx3)', fontSize: 13 }}>Nenhuma conta ativa.</p>}
        {ativas.map(c => (
          <div key={c.id} className="cb-conta-item">
            {editId === c.id ? (
              <div className="cb-edit-form">
                <div className="conta-form-row">
                  <input
                    value={editNome}
                    onChange={e => setEditNome(e.target.value)}
                    placeholder="Nome"
                    style={{ flex: 3 }}
                  />
                  <select value={editTipo} onChange={e => setEditTipo(e.target.value)} style={{ flex: 1, minWidth: 160 }}>
                    {TIPOS.map(t => <option key={t} value={t}>{TIPO_LABEL[t]}</option>)}
                  </select>
                  <div className="val-input-wrap" style={{ flex: 1, minWidth: 130 }}>
                    <span className="val-prefix">R$</span>
                    <input
                      type="text" inputMode="decimal" placeholder="Saldo inicial"
                      value={editSaldoDisplay}
                      onChange={e => {
                        const raw = e.target.value.replace(/[^\d,]/g, '')
                        setEditSaldoDisplay(raw)
                        setEditSaldo(parseBRL(raw))
                      }}
                      onBlur={() => setEditSaldoDisplay(editSaldo ? fmtNum(editSaldo) : '')}
                    />
                  </div>
                </div>
                <div style={{ display: 'flex', gap: 8, marginTop: 8 }}>
                  <button className="btn-add-conta" onClick={handleSalvarEdit} disabled={salvandoEdit}>
                    {salvandoEdit ? 'Salvando...' : '✔ Salvar'}
                  </button>
                  <button
                    onClick={() => setEditId(null)}
                    style={{ padding: '8px 16px', borderRadius: 8, border: '1px solid var(--bd)', background: 'var(--bg-card)', color: 'var(--tx1)', cursor: 'pointer', fontSize: 13 }}>
                    Cancelar
                  </button>
                </div>
              </div>
            ) : (
              <div className="cb-conta-clicavel" style={{ cursor: 'default' }}>
                <div className="cb-conta-info">
                  <div className="cb-conta-nome">{c.nome}</div>
                  <div className="cb-conta-meta">
                    {TIPO_LABEL[c.tipo] ?? c.tipo}
                    {c.saldoInicial > 0 && ` · Saldo inicial: ${fmtBRL(c.saldoInicial)}`}
                  </div>
                </div>
                <div className="cb-conta-saldo val-green">{fmtBRL(c.saldoAtual)}</div>
                <div className="cb-conta-acoes">
                  <button className="cb-btn-editar" onClick={() => iniciarEdicao(c)}>Editar</button>
                  <button className="cb-btn-inativar" onClick={() => handleInativar(c.id)}>Inativar</button>
                </div>
              </div>
            )}
          </div>
        ))}
      </div>

      {inativas.length > 0 && (
        <div className="contas-section">
          <h3 style={{ color: 'var(--tx3)' }}>⛔ Contas Inativas ({inativas.length})</h3>
          {inativas.map(c => (
            <div key={c.id} className="cb-conta-item cb-inativa">
              <div className="cb-conta-info">
                <div className="cb-conta-nome" style={{ color: 'var(--tx3)' }}>{c.nome}</div>
                <div className="cb-conta-meta">{TIPO_LABEL[c.tipo] ?? c.tipo} · Inativa</div>
              </div>
              <div className="cb-conta-saldo" style={{ color: 'var(--tx3)' }}>{fmtBRL(c.saldoAtual)}</div>
            </div>
          ))}
        </div>
      )}
    </>
  )
}
