import { useState, useEffect, useCallback } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { obterMeuFuturo } from '../../api/projecao'
import { obterPreferenciasFuturo, atualizarPreferenciasFuturo } from '../../api/preferenciasFuturo'
import { fmtBRL, monthLabel } from '../../utils/format'
import type { MeuFuturo } from '../../api/projecao'
import type { PreferenciasFuturo } from '../../api/preferenciasFuturo'
import './ClientMeuFuturo.css'

function fmtNum(n: number) {
  return n.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}
function parseBRL(s: string): number {
  const n = parseFloat(s.replace(/\./g, '').replace(',', '.'))
  return Number.isFinite(n) ? n : 0
}

const STATUS_LABEL: Record<string, string> = { Tranquilo: '🟢 Tranquilo', Atencao: '🟡 Atenção', Risco: '🔴 Risco' }
const STATUS_CLASSE: Record<string, string> = { Tranquilo: 'mf-status-tranquilo', Atencao: 'mf-status-atencao', Risco: 'mf-status-risco' }

export default function ClientMeuFuturoPage() {
  const { user } = useAuth()
  const clienteId = user?.usuarioId ?? ''

  const [preferencias, setPreferencias] = useState<PreferenciasFuturo>({})
  const [reservaDisplay, setReservaDisplay] = useState('')
  const [salvandoPreferencias, setSalvandoPreferencias] = useState(false)
  const [msgPreferencias, setMsgPreferencias] = useState('')

  // Simulador "E se...?" — item 3 do plano, pós-processamento sobre a tabela já calculada.
  const [ajusteReceitaPercent, setAjusteReceitaPercent] = useState(0)
  const [atrasoRecebimentosDias, setAtrasoRecebimentosDias] = useState(0)

  const [meuFuturo, setMeuFuturo] = useState<MeuFuturo | null>(null)
  const [loading, setLoading] = useState(true)
  const [erro, setErro] = useState('')

  useEffect(() => {
    if (!clienteId) return
    obterPreferenciasFuturo(clienteId).then(p => {
      setPreferencias(p)
      setReservaDisplay(p.reservaMinima ? fmtNum(p.reservaMinima) : '')
    }).catch(() => {})
  }, [clienteId])

  const carregar = useCallback(async () => {
    if (!clienteId) return
    setLoading(true); setErro('')
    try {
      const data = await obterMeuFuturo(clienteId, {
        reservaMinima: preferencias.reservaMinima,
        ajusteReceitaPercent: ajusteReceitaPercent || undefined,
        atrasoRecebimentosDias: atrasoRecebimentosDias || undefined,
      })
      setMeuFuturo(data)
    } catch (e: unknown) {
      setErro(e instanceof Error ? e.message : 'Erro ao carregar "Meu Futuro".')
    } finally {
      setLoading(false)
    }
  }, [clienteId, preferencias.reservaMinima, ajusteReceitaPercent, atrasoRecebimentosDias])

  useEffect(() => { carregar() }, [carregar])

  async function salvarPreferencias(tipoPessoa?: 'PF' | 'PJ') {
    if (!clienteId) return
    setSalvandoPreferencias(true); setMsgPreferencias('')
    try {
      const dto = { reservaMinima: parseBRL(reservaDisplay) || undefined, tipoPessoa: tipoPessoa ?? preferencias.tipoPessoa }
      const atualizado = await atualizarPreferenciasFuturo(clienteId, dto)
      setPreferencias(atualizado)
      setMsgPreferencias('Preferências salvas!')
    } catch (e: unknown) {
      setMsgPreferencias(e instanceof Error ? e.message : 'Erro ao salvar preferências.')
    } finally {
      setSalvandoPreferencias(false)
    }
  }

  const ehPF = preferencias.tipoPessoa === 'PF'
  const rotuloCaixa = ehPF ? 'sua renda' : 'seu negócio'

  return (
    <div className="mf-page">
      <div className="mf-header">
        <div>
          <h2 className="mf-titulo">🔮 Meu Futuro</h2>
          <div className="mf-subtitulo">Como {rotuloCaixa} deve se comportar nos próximos 12 meses</div>
        </div>
      </div>

      <div className="mf-preferencias">
        <label className="mf-pref-item">
          Reserva mínima
          <div className="val-input-wrap">
            <span className="val-prefix">R$</span>
            <input
              type="text" inputMode="decimal" placeholder="0,00"
              value={reservaDisplay}
              onChange={e => setReservaDisplay(e.target.value.replace(/[^\d,]/g, ''))}
              onBlur={() => salvarPreferencias()}
            />
          </div>
        </label>
        <div className="mf-pref-item">
          Eu sou
          <div className="mf-pf-pj-toggle">
            <button
              className={preferencias.tipoPessoa !== 'PF' ? 'active' : ''}
              disabled={salvandoPreferencias}
              onClick={() => salvarPreferencias('PJ')}
            >
              Pessoa Jurídica
            </button>
            <button
              className={preferencias.tipoPessoa === 'PF' ? 'active' : ''}
              disabled={salvandoPreferencias}
              onClick={() => salvarPreferencias('PF')}
            >
              Pessoa Física
            </button>
          </div>
        </div>
        {msgPreferencias && <span className="mf-pref-msg">{msgPreferencias}</span>}
      </div>

      {loading && <p className="mf-loading">Calculando...</p>}
      {erro && <p className="mf-erro">{erro}</p>}

      {meuFuturo && !loading && (
        <>
          {meuFuturo.conselhos.length > 0 && (
            <div className="mf-conselhos">
              {meuFuturo.conselhos.map((c, i) => <p key={i}>💬 {c}</p>)}
            </div>
          )}

          <div className="mf-tabela-container">
            <table className="mf-tabela">
              <thead>
                <tr>
                  <th>Mês</th>
                  <th>Saldo inicial</th>
                  <th>A receber</th>
                  <th>A pagar</th>
                  <th>Saldo final</th>
                  <th>Precisa receber</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {meuFuturo.meses.map(m => (
                  <tr key={m.mes} className={STATUS_CLASSE[m.status]}>
                    <td>{monthLabel(m.mes)}</td>
                    <td>{fmtBRL(m.saldoInicial)}</td>
                    <td className="val-green">+{fmtBRL(m.aReceber)}</td>
                    <td className="val-red">-{fmtBRL(m.aPagar)}</td>
                    <td className={m.saldoFinal >= 0 ? 'val-green' : 'val-red'}>{fmtBRL(m.saldoFinal)}</td>
                    <td>{m.precisaReceber > 0 ? fmtBRL(m.precisaReceber) : '—'}</td>
                    <td>{STATUS_LABEL[m.status]}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="mf-simulador">
            <h3 className="mf-simulador-titulo">🎛️ E se...?</h3>
            <div className="mf-simulador-controles">
              <label>
                Receita variar em <strong>{ajusteReceitaPercent > 0 ? '+' : ''}{ajusteReceitaPercent}%</strong>
                <input
                  type="range" min={-30} max={30} step={5}
                  value={ajusteReceitaPercent}
                  onChange={e => setAjusteReceitaPercent(Number(e.target.value))}
                />
              </label>
              <label>
                Atraso nos recebimentos: <strong>{atrasoRecebimentosDias} dia(s)</strong>
                <input
                  type="range" min={0} max={60} step={5}
                  value={atrasoRecebimentosDias}
                  onChange={e => setAtrasoRecebimentosDias(Number(e.target.value))}
                />
              </label>
              {(ajusteReceitaPercent !== 0 || atrasoRecebimentosDias !== 0) && (
                <button className="btn-cancel" onClick={() => { setAjusteReceitaPercent(0); setAtrasoRecebimentosDias(0) }}>
                  Limpar simulação
                </button>
              )}
            </div>
          </div>
        </>
      )}
    </div>
  )
}
