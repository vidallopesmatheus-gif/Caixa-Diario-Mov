import { useState, useEffect, useCallback } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'
import { listarContasBancarias } from '../../api/contasBancarias'
import { listarFaturasCartao } from '../../api/faturasCartao'
import type { FaturaCartao } from '../../api/faturasCartao'
import { fmtBRL } from '../../utils/format'
import type { ContaBancaria } from '../../types'
import './ClientContasBancarias.css'
import './ClientContaDetalhe.css'

interface Props { clienteIdOverride?: string }

const STATUS_COR: Record<FaturaCartao['status'], string> = {
  Aberta: 'var(--tx3)',
  Fechada: 'var(--warning)',
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
  const [faturas, setFaturas] = useState<FaturaCartao[]>([])
  const [loading, setLoading] = useState(true)
  const [msg, setMsg] = useState('')

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
      setFaturas(lista)
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao carregar faturas.')
    } finally {
      setLoading(false)
    }
  }, [clienteId, contaId])

  useEffect(() => { carregar() }, [carregar])

  if (loading) return <p style={{ color: 'var(--tx3)' }}>Carregando...</p>

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
        <div className="contas-section">
          {faturas.map(f => (
            <div key={f.competencia} className="cb-conta-item">
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
            </div>
          ))}
        </div>
      )}
    </>
  )
}
