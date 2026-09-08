import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'
import { listarContasBancarias } from '../../api/contasBancarias'
import { fmtBRL } from '../../utils/format'
import type { ContaBancaria } from '../../types'
import './ClientContasBancarias.css'

interface Props { clienteIdOverride?: string }

const TIPO_LABEL: Record<string, string> = {
  Caixa: '💵 Caixa',
  ContaCorrente: '🏦 Conta Corrente',
  Investimento: '📈 Investimento',
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

  const carregar = useCallback(() => {
    if (!clienteId) return
    setLoading(true)
    listarContasBancarias(clienteId)
      .then(setContas)
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [clienteId])

  useEffect(() => { carregar() }, [carregar])

  const ativas = contas.filter(c => c.ativa)
  const inativas = contas.filter(c => !c.ativa)
  const saldoConsolidado = ativas.reduce((s, c) => s + c.saldoAtual, 0)

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
      </div>

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
    </>
  )
}
