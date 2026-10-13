import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'
import { listarContasBancarias } from '../../api/contasBancarias'
import {
  listarSugestoesTransferenciaSemPar, descartarSugestaoTransferencia, converterLancamentoEmTransferencia,
} from '../../api/transferencias'
import type { SugestaoTransferenciaSemPar } from '../../api/transferencias'
import { limparDescricao } from '../../utils/descricaoExibicao'
import { fmtBRL, fmtDate } from '../../utils/format'
import type { ContaBancaria } from '../../types'
import './configuracoes/Categorias.css'
import './ClientContasBancarias.css'
import './ClientContaDetalhe.css'

/**
 * Money Vision — Bloco 4, item 4 da Parte 2: lançamentos que a importação já suspeita serem
 * transferência entre contas do próprio cliente (dicionário de palavras-chave — "Aplicação RDB",
 * "Resgate", "Caixinha"...) mas que ainda não foram confirmados. Confirmar aqui reaproveita o
 * mesmo fluxo de "⇄ marcar como transferência" do extrato (ConverterLancamentoAsync); descartar
 * só limpa a sugestão, o lançamento volta a ser um pendente comum.
 */
export default function ClientTransferenciasSemParPage() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const clienteId = user?.usuarioId ?? ''

  const [sugestoes, setSugestoes] = useState<SugestaoTransferenciaSemPar[]>([])
  const [contas, setContas] = useState<ContaBancaria[]>([])
  const [loading, setLoading] = useState(true)
  const [msg, setMsg] = useState('')
  const [msgOk, setMsgOk] = useState(true)
  const [contaEscolhidaPorItem, setContaEscolhidaPorItem] = useState<Record<string, string>>({})
  const [processandoId, setProcessandoId] = useState<string | null>(null)

  const carregar = useCallback(() => {
    if (!clienteId) return
    setLoading(true)
    Promise.all([listarSugestoesTransferenciaSemPar(clienteId), listarContasBancarias(clienteId)])
      .then(([s, c]) => {
        setSugestoes(s)
        setContas(c)
        setContaEscolhidaPorItem(prev => {
          const next = { ...prev }
          for (const item of s) {
            if (!(item.lancamentoId in next) && item.contaDestinoSugeridaId) next[item.lancamentoId] = item.contaDestinoSugeridaId
          }
          return next
        })
      })
      .catch(() => setMsg('Erro ao carregar as sugestões de transferência.'))
      .finally(() => setLoading(false))
  }, [clienteId])

  useEffect(() => { carregar() }, [carregar])

  function showMsg(texto: string, ok = true) {
    setMsgOk(ok)
    setMsg(texto)
    setTimeout(() => setMsg(''), 4000)
  }

  async function handleConfirmar(item: SugestaoTransferenciaSemPar) {
    const contaDestinoId = contaEscolhidaPorItem[item.lancamentoId]
    if (!contaDestinoId) return
    setProcessandoId(item.lancamentoId)
    try {
      await converterLancamentoEmTransferencia({
        contaId: item.contaBancariaId, lancamentoId: item.lancamentoId, data: item.data,
        tipo: item.tipo, contaContrapartidaId: contaDestinoId,
      })
      setSugestoes(prev => prev.filter(s => s.lancamentoId !== item.lancamentoId))
      showMsg('Confirmado como transferência.')
    } catch (e: unknown) {
      showMsg(e instanceof Error ? e.message : 'Erro ao confirmar transferência.', false)
    } finally {
      setProcessandoId(null)
    }
  }

  async function handleDescartar(item: SugestaoTransferenciaSemPar) {
    setProcessandoId(item.lancamentoId)
    try {
      await descartarSugestaoTransferencia({ contaId: item.contaBancariaId, lancamentoId: item.lancamentoId, data: item.data })
      setSugestoes(prev => prev.filter(s => s.lancamentoId !== item.lancamentoId))
      showMsg('Sugestão descartada — o lançamento continua pendente, pra categorizar manualmente.')
    } catch (e: unknown) {
      showMsg(e instanceof Error ? e.message : 'Erro ao descartar sugestão.', false)
    } finally {
      setProcessandoId(null)
    }
  }

  if (loading) return <p style={{ color: 'var(--tx3)' }}>Carregando...</p>

  return (
    <>
      <button className="cd-btn-voltar" onClick={() => navigate('/banco')}>← Voltar</button>
      <h2 className="cd-titulo">🔁 Transferências sem par</h2>
      <p style={{ color: 'var(--tx3)', fontSize: 13, marginBottom: 16 }}>
        Lançamentos que parecem ser transferência entre suas próprias contas (ex.: "Aplicação RDB",
        "Resgate"), mas que ainda não foram confirmados. Escolha a conta de destino e confirme, ou
        descarte se não for o caso — o lançamento volta a ser um pendente comum.
      </p>

      {msg && <div style={{ marginBottom: 12, fontSize: 13, fontWeight: 600, color: msgOk ? '#34c759' : '#ff6b6b' }}>{msg}</div>}

      {sugestoes.length === 0 ? (
        <p style={{ color: 'var(--tx3)', fontSize: 13 }}>Nenhuma sugestão pendente — tudo pareado.</p>
      ) : (
        <div className="cat-lista-compacta">
          {sugestoes.map(item => {
            const { titulo } = limparDescricao(item.descricao)
            const processando = processandoId === item.lancamentoId
            return (
              <div key={item.lancamentoId} className="cat-item-compacta">
                <span className={item.tipo === 'Entrada' ? 'cat-direcao cat-direcao-entrada' : 'cat-direcao cat-direcao-saida'} title={item.tipo}>
                  {item.tipo === 'Entrada' ? '↓' : '↑'}
                </span>
                <div style={{ flex: 1, minWidth: 220 }}>
                  <div className="cat-nome-compacta">{titulo}</div>
                  <div style={{ fontSize: 12, color: 'var(--tx3)' }}>
                    {item.contaBancariaNome} · {fmtDate(item.data)} · {fmtBRL(Math.abs(item.valor))}
                  </div>
                </div>
                <select
                  value={contaEscolhidaPorItem[item.lancamentoId] ?? ''}
                  onChange={e => setContaEscolhidaPorItem(prev => ({ ...prev, [item.lancamentoId]: e.target.value }))}
                  disabled={processando}
                >
                  <option value="">Para/De qual conta?</option>
                  {contas.filter(c => c.ativa && c.id !== item.contaBancariaId).map(c => (
                    <option key={c.id} value={c.id}>{c.nome}</option>
                  ))}
                </select>
                <div className="cat-acoes-compactas">
                  <button
                    className="cb-btn-editar"
                    onClick={() => handleConfirmar(item)}
                    disabled={!contaEscolhidaPorItem[item.lancamentoId] || processando}
                  >
                    {processando ? 'Confirmando...' : '✔ Confirmar'}
                  </button>
                  <button className="cat-btn-excluir" onClick={() => handleDescartar(item)} disabled={processando}>
                    Descartar
                  </button>
                </div>
              </div>
            )
          })}
        </div>
      )}
    </>
  )
}
