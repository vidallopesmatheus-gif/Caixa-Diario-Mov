import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'
import { listarContasBancarias } from '../../api/contasBancarias'
import { criarTransferencia, listarTransferencias, estornarTransferencia } from '../../api/transferencias'
import type { Transferencia } from '../../api/transferencias'
import { fmtBRL, fmtDate, todayISO } from '../../utils/format'
import Modal from '../../components/shared/Modal'
import type { ContaBancaria } from '../../types'
import './ClientContasBancarias.css'

interface Props { clienteIdOverride?: string }

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

/** Aba "Banco": só operação (extrato, importação, transferência). Cadastro de contas vive em Configurações. */
export default function ClientBancoPage({ clienteIdOverride }: Props) {
  const { user } = useAuth()
  const navigate = useNavigate()
  const clienteId = clienteIdOverride ?? user?.usuarioId ?? null

  const [contas, setContas] = useState<ContaBancaria[]>([])
  const [transferencias, setTransferencias] = useState<Transferencia[]>([])
  const [loading, setLoading] = useState(true)
  const [msg, setMsg] = useState('')
  const [msgOk, setMsgOk] = useState(true)

  const [modalAberto, setModalAberto] = useState(false)
  const [origemId, setOrigemId] = useState('')
  const [destinoId, setDestinoId] = useState('')
  const [valorDisplay, setValorDisplay] = useState('')
  const [valor, setValor] = useState(0)
  const [data, setData] = useState(todayISO())
  const [descricao, setDescricao] = useState('')
  const [transferindo, setTransferindo] = useState(false)

  const carregar = useCallback(() => {
    if (!clienteId) return
    setLoading(true)
    Promise.all([listarContasBancarias(clienteId), listarTransferencias(clienteId)])
      .then(([cs, ts]) => { setContas(cs); setTransferencias(ts) })
      .catch(() => showMsg('Erro ao carregar contas.', false))
      .finally(() => setLoading(false))
  }, [clienteId])

  useEffect(() => { carregar() }, [carregar])

  function showMsg(texto: string, ok = true) {
    setMsgOk(ok)
    setMsg(texto)
    setTimeout(() => setMsg(''), 4000)
  }

  function abrirModalTransferir() {
    setOrigemId(''); setDestinoId(''); setValorDisplay(''); setValor(0); setData(todayISO()); setDescricao('')
    setModalAberto(true)
  }

  async function handleTransferir() {
    if (!clienteId || !origemId || !destinoId || valor <= 0) return
    setTransferindo(true)
    try {
      await criarTransferencia({ clienteId, contaOrigemId: origemId, contaDestinoId: destinoId, data, valor, descricao: descricao || undefined })
      setModalAberto(false)
      showMsg('Transferência realizada com sucesso!')
      carregar()
    } catch (e: unknown) {
      showMsg(e instanceof Error ? e.message : 'Erro ao transferir.', false)
    } finally {
      setTransferindo(false)
    }
  }

  async function handleEstornar(id: string) {
    if (!confirm('Estornar esta transferência? As duas pontas (origem e destino) serão removidas.')) return
    try {
      await estornarTransferencia(id)
      showMsg('Transferência estornada.')
      carregar()
    } catch (e: unknown) {
      showMsg(e instanceof Error ? e.message : 'Erro ao estornar.', false)
    }
  }

  const ativas = contas.filter(c => c.ativa)
  const inativas = contas.filter(c => !c.ativa)
  const saldoConsolidado = ativas.reduce((s, c) => s + c.saldoAtual, 0)
  const podeTransferir = !!origemId && !!destinoId && origemId !== destinoId && valor > 0

  if (loading) return <p style={{ color: 'var(--tx3)' }}>Carregando...</p>

  return (
    <>
      <div className="cb-resumo">
        <div className="cb-resumo-item">
          <span className="cb-resumo-label">Contas ativas</span>
          <span className="cb-resumo-val">{ativas.length}</span>
        </div>
        <div className="cb-resumo-item">
          <span className="cb-resumo-label">Saldo consolidado</span>
          <span className="cb-resumo-val val-green">{fmtBRL(saldoConsolidado)}</span>
        </div>
        <button className="btn-add-conta" onClick={abrirModalTransferir} disabled={ativas.length < 2}
          title={ativas.length < 2 ? 'É preciso ao menos 2 contas ativas' : ''}>
          🔁 Transferir
        </button>
      </div>

      {msg && (
        <div style={{ marginBottom: 12, fontSize: 13, fontWeight: 600, color: msgOk ? '#34c759' : '#ff6b6b' }}>
          {msg}
        </div>
      )}

      <div className="contas-section">
        <h3>🏦 Contas Ativas ({ativas.length})</h3>
        {ativas.length === 0 && (
          <p style={{ color: 'var(--tx3)', fontSize: 13 }}>
            Nenhuma conta ativa. Cadastre uma em Configurações → Contas Bancárias.
          </p>
        )}
        {ativas.map(c => (
          <div key={c.id} className="cb-conta-item">
            <div
              className="cb-conta-clicavel"
              role="button"
              tabIndex={0}
              onClick={() => navigate(`/banco/${c.id}`)}
              onKeyDown={e => { if (e.key === 'Enter') navigate(`/banco/${c.id}`) }}
            >
              <div className="cb-conta-info">
                <div className="cb-conta-nome">{c.nome}</div>
                <div className="cb-conta-meta">
                  {TIPO_LABEL[c.tipo] ?? c.tipo}
                  {c.saldoInicial > 0 && ` · Saldo inicial: ${fmtBRL(c.saldoInicial)}`}
                </div>
                <div className="cb-conta-resumo-mes">
                  Este mês: <span className="val-green">+{fmtBRL(c.entradasMes)}</span>
                  {' · '}
                  <span className="val-red">-{fmtBRL(c.saidasMes)}</span>
                  {c.pendentesCategorizacao > 0 && (
                    <> · <span style={{ color: 'var(--warning)' }}>🏷️ {c.pendentesCategorizacao} pendente(s)</span></>
                  )}
                </div>
              </div>
              <div className="cb-conta-saldo val-green">{fmtBRL(c.saldoAtual)}</div>
            </div>
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

      {transferencias.length > 0 && (
        <div className="contas-section">
          <h3>🔁 Transferências recentes</h3>
          {transferencias.slice(0, 15).map(t => (
            <div key={t.id} className="cb-conta-item">
              <div className="cb-conta-info">
                <div className="cb-conta-nome">{t.contaOrigemNome} → {t.contaDestinoNome}</div>
                <div className="cb-conta-meta">
                  {fmtDate(t.data)}{t.descricao ? ` · ${t.descricao}` : ''}
                </div>
              </div>
              <div className="cb-conta-saldo">{fmtBRL(t.valor)}</div>
              <div className="cb-conta-acoes">
                <button className="cb-btn-inativar" onClick={() => handleEstornar(t.id)}>Estornar</button>
              </div>
            </div>
          ))}
        </div>
      )}

      <Modal
        open={modalAberto}
        title="🔁 Transferir entre contas"
        onClose={() => setModalAberto(false)}
        footer={
          <>
            <button className="btn-cancel" onClick={() => setModalAberto(false)}>Cancelar</button>
            <button className="btn-confirm" onClick={handleTransferir} disabled={!podeTransferir || transferindo}>
              {transferindo ? 'Transferindo...' : 'Confirmar transferência'}
            </button>
          </>
        }
      >
        <div className="inp-group">
          <label>Conta de origem</label>
          <select value={origemId} onChange={e => setOrigemId(e.target.value)}>
            <option value="">Selecione...</option>
            {ativas.map(c => <option key={c.id} value={c.id} disabled={c.id === destinoId}>{c.nome} ({fmtBRL(c.saldoAtual)})</option>)}
          </select>
        </div>
        <div className="inp-group">
          <label>Conta de destino</label>
          <select value={destinoId} onChange={e => setDestinoId(e.target.value)}>
            <option value="">Selecione...</option>
            {ativas.map(c => <option key={c.id} value={c.id} disabled={c.id === origemId}>{c.nome} ({fmtBRL(c.saldoAtual)})</option>)}
          </select>
        </div>
        <div className="inp-group">
          <label>Valor</label>
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
        <div className="inp-group">
          <label>Data</label>
          <input type="date" value={data} max={todayISO()} onChange={e => setData(e.target.value)} />
        </div>
        <div className="inp-group">
          <label>Descrição (opcional)</label>
          <input value={descricao} onChange={e => setDescricao(e.target.value)} placeholder="Ex: Aporte mensal" />
        </div>
      </Modal>
    </>
  )
}
