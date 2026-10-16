import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'
import { listarContasBancarias } from '../../api/contasBancarias'
import { listarSugestoesTransferenciaSemPar } from '../../api/transferencias'
import { obterParaOndeFoiMeuDinheiro } from '../../api/metricas'
import type { ParaOndeFoiMeuDinheiro } from '../../api/metricas'
import ParaOndeFoiMeuDinheiroCard from '../../components/shared/ParaOndeFoiMeuDinheiroCard'
import { fmtBRL } from '../../utils/format'
import type { ContaBancaria } from '../../types'
import './ClientContasBancarias.css'

interface Props { clienteIdOverride?: string }

const TIPO_LABEL: Record<string, string> = {
  Caixa: '💵 Caixa',
  ContaCorrente: '🏦 Conta Corrente',
  Investimento: '📈 Investimento',
  CartaoCredito: '💳 Cartão de crédito',
}

/**
 * Aba "Banco": só operação (extrato, importação). Cadastro de contas vive em Configurações.
 * Transferência não se cria mais aqui — o dinheiro já se moveu no banco antes do app existir;
 * transferência é uma classificação de lançamento (extrato da conta, ou em lote na tela de
 * Categorizar Lançamentos), não um lançamento criado do zero.
 */
export default function ClientBancoPage({ clienteIdOverride }: Props) {
  const { user } = useAuth()
  const navigate = useNavigate()
  const clienteId = clienteIdOverride ?? user?.usuarioId ?? null

  const [contas, setContas] = useState<ContaBancaria[]>([])
  const [loading, setLoading] = useState(true)
  const [qtdSemPar, setQtdSemPar] = useState(0)
  const [paraOndeFoi, setParaOndeFoi] = useState<ParaOndeFoiMeuDinheiro | null>(null)

  const carregar = useCallback(() => {
    if (!clienteId) return
    setLoading(true)
    listarContasBancarias(clienteId)
      .then(setContas)
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [clienteId])

  useEffect(() => { carregar() }, [carregar])
  // Bloco 4, item 4 (Money Vision): avisa quantas sugestões de transferência estão sem par.
  useEffect(() => {
    if (!clienteId) return
    listarSugestoesTransferenciaSemPar(clienteId).then(s => setQtdSemPar(s.length)).catch(() => {})
  }, [clienteId])
  // Bloco 5 (Money Vision): card "Para onde foi meu dinheiro" do mês atual.
  useEffect(() => {
    if (!clienteId) return
    obterParaOndeFoiMeuDinheiro(clienteId).then(setParaOndeFoi).catch(() => {})
  }, [clienteId])

  const ativas = contas.filter(c => c.ativa)
  const inativas = contas.filter(c => !c.ativa)
  // Bloco 5, item 1 (Money Vision): "Disponível" deixou de somar investimento junto com conta
  // corrente — era exatamente isso que dava a falsa impressão de "sem dinheiro" quando na
  // verdade o saldo só estava guardado, não gasto. Agora são 3 cards: disponível, investido, patrimônio.
  // Cartão de crédito é dívida, não dinheiro disponível — não entra em nenhum dos cards do topo.
  // Rodada de ajustes Money Vision: o resumo de cartão sai do topo do Banco — fica só dentro da
  // própria conta "Cartões" (extrato + Ver faturas), evitando duplicar o mesmo dado em dois lugares.
  const contasDisponiveis = ativas.filter(c => c.tipo === 'ContaCorrente' || c.tipo === 'Caixa')
  const contasInvestimento = ativas.filter(c => c.tipo === 'Investimento')
  const saldoDisponivel = contasDisponiveis.reduce((s, c) => s + c.saldoAtual, 0)
  const saldoInvestido = contasInvestimento.reduce((s, c) => s + c.saldoAtual, 0)
  const patrimonioTotal = saldoDisponivel + saldoInvestido

  if (loading) return <p style={{ color: 'var(--tx3)' }}>Carregando...</p>

  return (
    <>
      <div className="cb-resumo">
        <div className="cb-resumo-item">
          <span className="cb-resumo-label">Contas ativas</span>
          <span className="cb-resumo-val">{ativas.length}</span>
        </div>
        <div className="cb-resumo-item">
          <span className="cb-resumo-label">Disponível para usar</span>
          <span className={`cb-resumo-val ${saldoDisponivel < 0 ? 'val-red' : 'val-green'}`}>{fmtBRL(saldoDisponivel)}</span>
        </div>
        {contasInvestimento.length > 0 && (
          <div className="cb-resumo-item">
            <span className="cb-resumo-label">Investido / Reserva</span>
            <span className="cb-resumo-val" style={{ color: 'var(--mv-blue-light)' }}>{fmtBRL(saldoInvestido)}</span>
          </div>
        )}
        <div className="cb-resumo-item">
          <span className="cb-resumo-label">Patrimônio total</span>
          <span className="cb-resumo-val">{fmtBRL(patrimonioTotal)}</span>
        </div>
      </div>

      {paraOndeFoi && <ParaOndeFoiMeuDinheiroCard dados={paraOndeFoi} />}

      {qtdSemPar > 0 && (
        <div
          className="cb-conta-item cb-conta-clicavel"
          role="button"
          tabIndex={0}
          onClick={() => navigate('/banco/transferencias-sem-par')}
          onKeyDown={e => { if (e.key === 'Enter') navigate('/banco/transferencias-sem-par') }}
          style={{ marginBottom: 16 }}
        >
          <div className="cb-conta-info">
            <div className="cb-conta-nome">🔁 Transferências sem par</div>
            <div className="cb-conta-meta">Lançamentos que parecem transferência, ainda não confirmados</div>
          </div>
          <div className="cb-conta-saldo" style={{ color: 'var(--warning)' }}>{qtdSemPar}</div>
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
              aria-label={`Abrir conta ${c.nome}`}
              onClick={() => navigate(`/banco/${c.id}`)}
              onKeyDown={e => { if (e.key === 'Enter') navigate(`/banco/${c.id}`) }}
            >
              <div className="cb-conta-info">
                <div className="cb-conta-nome">{c.nome}</div>
                <div className="cb-conta-meta">
                  {TIPO_LABEL[c.tipo] ?? c.tipo}
                  {c.tipo === 'CartaoCredito'
                    ? (c.limite ? ` · Limite: ${fmtBRL(c.limite)}` : '')
                    : (c.saldoInicial !== 0 && ` · Saldo inicial: ${fmtBRL(c.saldoInicial)}`)}
                </div>
                <div className="cb-conta-resumo-mes">
                  {/* Item 5 (Money Vision): investimento usa aporte/resgate — entradasMes/saidasMes
                      excluem Transferencia por design e sempre mostravam R$ 0,00 aqui. */}
                  {c.tipo === 'Investimento' ? (
                    <>
                      Este mês: <span className="val-green">+{fmtBRL(c.aporteMesAtual ?? 0)}</span>
                      {' · '}
                      <span className="val-red">-{fmtBRL(c.resgateMesAtual ?? 0)}</span>
                    </>
                  ) : (
                    <>
                      Este mês: <span className="val-green">+{fmtBRL(c.entradasMes)}</span>
                      {' · '}
                      <span className="val-red">-{fmtBRL(c.saidasMes)}</span>
                    </>
                  )}
                  {c.pendentesCategorizacao > 0 && (
                    <> · <span style={{ color: 'var(--warning)' }}>🏷️ {c.pendentesCategorizacao} pendente(s)</span></>
                  )}
                </div>
              </div>
              {c.tipo === 'CartaoCredito'
                ? <div className="cb-conta-saldo val-red">{fmtBRL(c.saldoDevedor ?? 0)}</div>
                : <div className={`cb-conta-saldo ${c.saldoAtual < 0 ? 'val-red' : 'val-green'}`}>{fmtBRL(c.saldoAtual)}</div>}
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
    </>
  )
}
