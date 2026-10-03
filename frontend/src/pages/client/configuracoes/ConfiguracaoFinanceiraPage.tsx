import { useState, useEffect } from 'react'
import { useAuth } from '../../../contexts/AuthContext'
import { obterConfiguracaoFinanceira, atualizarConfiguracaoFinanceira } from '../../../api/configuracaoFinanceira'

interface Props { clienteIdOverride?: string }

function fmtNum(n: number) {
  return n.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}
function parseBRL(s: string): number {
  return parseFloat(s.replace(/\./g, '').replace(',', '.')) || 0
}

export default function ConfiguracaoFinanceiraPage({ clienteIdOverride }: Props) {
  const { user } = useAuth()
  const clienteId = clienteIdOverride ?? user?.usuarioId ?? null

  const [loading, setLoading] = useState(true)
  const [salvando, setSalvando] = useState(false)
  const [msg, setMsg] = useState('')
  const [msgOk, setMsgOk] = useState(true)

  const [usaCustoManual, setUsaCustoManual] = useState(false)
  const [custoVidaDisplay, setCustoVidaDisplay] = useState('')
  const [taxaRetirada, setTaxaRetirada] = useState('4')

  useEffect(() => {
    if (!clienteId) return
    setLoading(true)
    obterConfiguracaoFinanceira(clienteId)
      .then(c => {
        setUsaCustoManual(c.custoVidaMensalManual != null)
        setCustoVidaDisplay(c.custoVidaMensalManual != null ? fmtNum(c.custoVidaMensalManual) : '')
        setTaxaRetirada(String(c.taxaRetiradaFire))
      })
      .catch(() => setMsg('Erro ao carregar configuração financeira.'))
      .finally(() => setLoading(false))
  }, [clienteId])

  async function handleSalvar() {
    if (!clienteId) return
    const taxa = parseFloat(taxaRetirada.replace(',', '.'))
    if (!taxa || taxa <= 0 || taxa > 100) {
      setMsg('Taxa de retirada deve estar entre 0 e 100%.')
      setMsgOk(false)
      return
    }
    setSalvando(true)
    setMsg('')
    try {
      await atualizarConfiguracaoFinanceira(clienteId, {
        custoVidaMensalManual: usaCustoManual ? parseBRL(custoVidaDisplay) : null,
        taxaRetiradaFire: taxa,
      })
      setMsg('Configuração salva!')
      setMsgOk(true)
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao salvar.')
      setMsgOk(false)
    } finally {
      setSalvando(false)
    }
  }

  if (loading) return <p style={{ color: 'var(--tx3)' }}>Carregando...</p>

  return (
    <>
      <h3 style={{ marginBottom: 16 }}>💰 Financeiro</h3>
      <p style={{ color: 'var(--tx3)', fontSize: 13, marginTop: -12, marginBottom: 16 }}>
        Usado pelo Indicador FIRE no Dashboard — nunca pela despesa total do negócio.
      </p>

      <div className="add-conta-form">
        <h4>Custo de vida mensal</h4>
        <div className="conta-form-row" style={{ flexDirection: 'column', alignItems: 'flex-start', gap: 8 }}>
          <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 13 }}>
            <input type="checkbox" checked={usaCustoManual} onChange={e => setUsaCustoManual(e.target.checked)} />
            Definir manualmente (em vez de calcular automaticamente)
          </label>
          {usaCustoManual ? (
            <input
              placeholder="Custo de vida mensal (R$)"
              value={custoVidaDisplay}
              onChange={e => setCustoVidaDisplay(e.target.value)}
              style={{ minWidth: 200 }}
            />
          ) : (
            <p style={{ fontSize: 12, color: 'var(--tx3)', margin: 0 }}>
              Modo automático: média das categorias marcadas "Custo de vida (FIRE)" no Plano de Contas,
              nos últimos 12 meses fechados (mínimo 3 meses com dado).
            </p>
          )}
        </div>

        <h4 style={{ marginTop: 20 }}>Taxa de retirada</h4>
        <div className="conta-form-row">
          <input
            placeholder="4"
            value={taxaRetirada}
            onChange={e => setTaxaRetirada(e.target.value)}
            style={{ maxWidth: 100 }}
          />
          <span style={{ fontSize: 12, color: 'var(--tx3)' }}>
            % ao ano — referência de mercado (regra dos 4%), não uma garantia. Taxas menores são mais conservadoras.
          </span>
        </div>

        <button className="btn-add-conta" onClick={handleSalvar} disabled={salvando} style={{ marginTop: 16 }}>
          {salvando ? 'Salvando...' : '✔ Salvar'}
        </button>
      </div>

      {msg && <p style={{ color: msgOk ? 'var(--success)' : 'var(--danger)', fontSize: 13 }}>{msg}</p>}
    </>
  )
}
