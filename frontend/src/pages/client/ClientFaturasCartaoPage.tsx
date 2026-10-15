import { useState, useEffect, useCallback } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'
import { listarContasBancarias } from '../../api/contasBancarias'
import { listarFaturasCartao, pagarFatura } from '../../api/faturasCartao'
import type { FaturaCartao } from '../../api/faturasCartao'
import { fmtBRL, todayISO } from '../../utils/format'
import Modal from '../../components/shared/Modal'
import type { ContaBancaria } from '../../types'
import './ClientContasBancarias.css'
import './ClientContaDetalhe.css'

function fmtNum(n: number) {
  if (!n) return ''
  return n.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}
function parseBRL(s: string): number {
  return parseFloat(s.replace(/\./g, '').replace(',', '.')) || 0
}

interface Props { clienteIdOverride?: string }

const STATUS_COR: Record<FaturaCartao['status'], string> = {
  Aberta: 'var(--tx3)',
  Fechada: 'var(--warning)',
  Parcial: 'var(--warning)',
  Paga: 'var(--success)',
}

function fmtDate(iso: string): string {
  return iso.slice(0, 10).split('-').reverse().join('/')
}

function fmtCompetencia(competencia: string): string {
  const [ano, mes] = competencia.split('-')
  const nomes = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez']
  return `${nomes[Number(mes) - 1]}/${ano}`
}

/** Item 9: tela dedicada listando as faturas de um cartão (aberta/fechada/paga), ciclo a ciclo —
 * reaproveita a mesma API já usada no modal "Pagamento de fatura" do Caixa. */
export default function ClientFaturasCartaoPage({ clienteIdOverride }: Props) {
  const { contaId } = useParams<{ contaId: string }>()
  const { user } = useAuth()
  const navigate = useNavigate()
  const clienteId = clienteIdOverride ?? user?.usuarioId ?? null

  const [conta, setConta] = useState<ContaBancaria | null>(null)
  const [contasOrigem, setContasOrigem] = useState<ContaBancaria[]>([])
  const [faturas, setFaturas] = useState<FaturaCartao[]>([])
  const [loading, setLoading] = useState(true)
  const [msg, setMsg] = useState('')
  const [pagarAlvo, setPagarAlvo] = useState<FaturaCartao | null>(null)

  const carregar = useCallback(async () => {
    if (!clienteId || !contaId) return
    setLoading(true)
    setMsg('')
    try {
      const [contas, lista] = await Promise.all([
        listarContasBancarias(clienteId),
        listarFaturasCartao(contaId),
      ])
      setConta(contas.find(c => c.id === contaId) ?? null)
      setContasOrigem(contas.filter(c => c.ativa && c.tipo !== 'CartaoCredito'))
      setFaturas(lista)
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao carregar faturas.')
    } finally {
      setLoading(false)
    }
  }, [clienteId, contaId])

  useEffect(() => { carregar() }, [carregar])

  if (loading) return <p style={{ color: 'var(--tx3)' }}>Carregando...</p>

  // Item 8: agrupa por banco/emissor — a conta "Cartões" é única pra todos os cartões, então sem
  // isso as faturas de bancos diferentes ficariam misturadas numa lista só.
  const porBanco = new Map<string, FaturaCartao[]>()
  for (const f of faturas) {
    const chave = f.banco ?? 'Sem banco informado'
    const grupo = porBanco.get(chave) ?? []
    grupo.push(f)
    porBanco.set(chave, grupo)
  }

  return (
    <>
      <div className="cd-header">
        <div>
          <button className="cd-btn-voltar" onClick={() => navigate(`/banco/${contaId}`)}>← Voltar</button>
          <h2 className="cd-titulo">💳 Faturas · {conta?.nome ?? ''}</h2>
          <div className="cd-subtitulo">Uma linha por ciclo de fechamento, mais recente primeiro</div>
        </div>
      </div>

      {msg && <div className="cd-msg cd-msg-aviso">{msg}</div>}

      {faturas.length === 0 ? (
        <p style={{ color: 'var(--tx3)', fontSize: 13 }}>Nenhuma fatura encontrada nesta conta ainda.</p>
      ) : (
        Array.from(porBanco.entries()).map(([banco, faturasDoBanco]) => (
          <div key={banco} style={{ marginBottom: 16 }}>
            <div style={{ fontSize: 13, fontWeight: 700, color: 'var(--tx2)', margin: '12px 0 6px' }}>{banco}</div>
            <div className="contas-section">
              {faturasDoBanco.map(f => (
                <div key={f.id ?? f.competencia} className="cb-conta-item">
                  <div className="cb-conta-info">
                    <div className="cb-conta-nome">
                      {fmtCompetencia(f.competencia)}
                      <span style={{
                        marginLeft: 8, fontSize: 11, fontWeight: 700, textTransform: 'uppercase',
                        color: STATUS_COR[f.status], border: `1px solid ${STATUS_COR[f.status]}`,
                        borderRadius: 999, padding: '1px 8px',
                      }}>
                        {f.status}
                      </span>
                    </div>
                    <div className="cb-conta-meta">
                      Fecha {fmtDate(f.dataFechamento)} · Vence {fmtDate(f.dataVencimento)} · {f.quantidadeCompras} compra(s)
                    </div>
                    <div className="cb-conta-resumo-mes">
                      Total: {fmtBRL(f.valorTotal)}
                      {f.valorPago > 0 && <> · Pago: <span className="val-green">{fmtBRL(f.valorPago)}</span></>}
                    </div>
                  </div>
                  <div className={`cb-conta-saldo ${f.saldoDevedor > 0 ? 'val-red' : 'val-green'}`}>
                    {fmtBRL(f.saldoDevedor)}
                  </div>
                  {f.status !== 'Paga' && (
                    <div className="cb-conta-acoes">
                      <button className="cb-btn-editar" onClick={() => setPagarAlvo(f)}>Pagar</button>
                    </div>
                  )}
                </div>
              ))}
            </div>
          </div>
        ))
      )}

      {pagarAlvo && contaId && (
        <PagarFaturaModal
          contaCartaoId={contaId}
          fatura={pagarAlvo}
          contasOrigem={contasOrigem}
          onClose={() => setPagarAlvo(null)}
          onPago={async () => { setPagarAlvo(null); await carregar() }}
        />
      )}
    </>
  )
}

function PagarFaturaModal({ contaCartaoId, fatura, contasOrigem, onClose, onPago }: {
  contaCartaoId: string
  fatura: FaturaCartao
  contasOrigem: ContaBancaria[]
  onClose: () => void
  onPago: () => void
}) {
  const [contaOrigemId, setContaOrigemId] = useState(contasOrigem[0]?.id ?? '')
  const [data, setData] = useState(todayISO())
  const [valorDisplay, setValorDisplay] = useState(fmtNum(fatura.saldoDevedor))
  const [erro, setErro] = useState('')
  const [salvando, setSalvando] = useState(false)

  const handlePagar = async () => {
    setErro('')
    const valor = parseBRL(valorDisplay)
    if (!contaOrigemId) { setErro('Selecione a conta de origem.'); return }
    if (valor <= 0) { setErro('Informe um valor maior que zero.'); return }
    setSalvando(true)
    try {
      await pagarFatura(contaCartaoId, { contaOrigemId, data, competencia: fatura.competencia, faturaId: fatura.id, valor })
      onPago()
    } catch (e: unknown) {
      setErro(e instanceof Error ? e.message : 'Erro ao pagar fatura.')
    } finally {
      setSalvando(false)
    }
  }

  return (
    <Modal open title={`Pagar fatura ${fmtCompetencia(fatura.competencia)}`} onClose={onClose} footer={
      <>
        <button className="btn-cancel" onClick={onClose}>Cancelar</button>
        <button className="btn-primary" onClick={handlePagar} disabled={salvando}>
          {salvando ? 'Pagando...' : 'Confirmar pagamento'}
        </button>
      </>
    }>
      {erro && <div className="cd-msg cd-msg-aviso">{erro}</div>}
      <div className="inp-group">
        <label>Conta de origem</label>
        <select value={contaOrigemId} onChange={e => setContaOrigemId(e.target.value)}>
          <option value="">Selecione...</option>
          {contasOrigem.map(c => <option key={c.id} value={c.id}>{c.nome}</option>)}
        </select>
      </div>
      <div className="inp-group">
        <label>Data</label>
        <input type="date" value={data} onChange={e => setData(e.target.value)} />
      </div>
      <div className="inp-group">
        <label>Valor (saldo devedor: {fmtBRL(fatura.saldoDevedor)})</label>
        <div className="val-input-wrap">
          <span className="val-prefix">R$</span>
          <input
            type="text" inputMode="decimal" value={valorDisplay}
            onChange={e => setValorDisplay(e.target.value)}
            onBlur={() => setValorDisplay(fmtNum(parseBRL(valorDisplay)))}
          />
        </div>
      </div>
    </Modal>
  )
}
