import { useAuth } from '../../contexts/AuthContext'
import { useRegistros } from '../../hooks/useRegistros'
import { fmtBRL, fmtDate, monthLabel, addMonths } from '../../utils/format'
import StatCard from '../../components/shared/StatCard'
import Modal from '../../components/shared/Modal'
import { ehOperacional } from '../../utils/lancamentos'
import { useState } from 'react'

interface Props { clienteIdOverride?: string }

export default function ClientHistoricoPage({ clienteIdOverride }: Props) {
  const { user } = useAuth()
  const clienteId = clienteIdOverride ?? user?.usuarioId ?? null
  const { registros, loading, excluir } = useRegistros(clienteId)
  const [openId, setOpenId] = useState<string | null>(null)
  const [delData, setDelData] = useState<string | null>(null)
  const [motivo, setMotivo] = useState('')
  const [erroDelete, setErroDelete] = useState('')

  const mesAtualReal = new Date().toISOString().slice(0, 7)
  const [mesSelecionado, setMesSelecionado] = useState(mesAtualReal)
  const doMes = registros.filter(r => r.data.startsWith(mesSelecionado))
  // Transferências entre contas e rendimento de investimento não são receita/despesa.
  const totalEnt = doMes.reduce((s, r) => s + r.entradas.filter(ehOperacional).reduce((a, x) => a + x.valor, 0), 0)
  const totalSai = doMes.reduce((s, r) => s + r.saidas.filter(ehOperacional).reduce((a, x) => a + x.valor, 0), 0)
  const ultimo = doMes[0]?.saldoConfirmado ?? 0
  const podeAvancar = mesSelecionado < mesAtualReal

  async function handleDelete() {
    if (!delData) return
    setErroDelete('')
    try { await excluir(delData, motivo); setDelData(null); setMotivo('') }
    catch (e: unknown) { setErroDelete(e instanceof Error ? e.message : String(e)) }
  }

  if (loading) return <p style={{ color: 'var(--tx3)' }}>Carregando...</p>

  return (
    <>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 10, marginBottom: 14 }}>
        <button
          aria-label="Mês anterior"
          onClick={() => setMesSelecionado(m => addMonths(m, -1))}
          style={{ background: 'var(--bg-card)', border: '1px solid var(--bd)', borderRadius: 8, color: 'var(--tx1)', fontSize: 16, width: 32, height: 32, cursor: 'pointer' }}
        >
          ‹
        </button>
        <input
          type="month"
          value={mesSelecionado}
          max={mesAtualReal}
          onChange={e => e.target.value && setMesSelecionado(e.target.value)}
          style={{ background: 'var(--bg-card)', border: '1px solid var(--bd)', borderRadius: 8, color: 'var(--tx1)', fontSize: 13, padding: '5px 10px' }}
        />
        <button
          aria-label="Próximo mês"
          disabled={!podeAvancar}
          onClick={() => setMesSelecionado(m => addMonths(m, 1))}
          style={{ background: 'var(--bg-card)', border: '1px solid var(--bd)', borderRadius: 8, color: podeAvancar ? 'var(--tx1)' : 'var(--tx3)', fontSize: 16, width: 32, height: 32, cursor: podeAvancar ? 'pointer' : 'default', opacity: podeAvancar ? 1 : 0.5 }}
        >
          ›
        </button>
      </div>

      <div className="stats-grid">
        <StatCard label="📅 Mês selecionado" value={monthLabel(mesSelecionado)} className="val-blue" />
        <StatCard label="📤 Total entradas" value={fmtBRL(totalEnt)} className="val-green" />
        <StatCard label="💸 Total saídas" value={fmtBRL(totalSai)} className="val-red" />
        <StatCard label="💰 Último saldo" value={fmtBRL(ultimo)} className="val-green" />
      </div>

      {doMes.length === 0 && (
        <p style={{ color: 'var(--tx3)', textAlign: 'center', marginTop: 24 }}>
          Nenhum lançamento em {monthLabel(mesSelecionado)}.
        </p>
      )}

      <div style={{ display: 'flex', flexDirection: 'column', gap: 6, marginTop: 8 }}>
        {doMes.map(r => {
          // Idem: o "lucro" do dia não deve contar transferências/rendimento — a lista de
          // lançamentos abaixo (quando expandida) continua mostrando cada um individualmente.
          const entTotal = r.entradas.filter(ehOperacional).reduce((a, x) => a + x.valor, 0)
          const saiTotal = r.saidas.filter(ehOperacional).reduce((a, x) => a + x.valor, 0)
          const lucro = entTotal - saiTotal
          const isOpen = openId === r.id

          return (
            <div key={r.id} style={{ background: 'var(--bg-card)', border: '1px solid var(--bd)', borderRadius: 10, overflow: 'hidden' }}>
              {/* Linha de relatório */}
              <div
                style={{ display: 'grid', gridTemplateColumns: '110px 1fr 1fr 1fr 90px 28px', alignItems: 'center', gap: 8, padding: '10px 14px', cursor: 'pointer', fontSize: 13 }}
                onClick={() => setOpenId(isOpen ? null : r.id)}
              >
                <span style={{ fontWeight: 600, color: 'var(--tx1)' }}>{fmtDate(r.data)}</span>
                <span style={{ color: '#34c759' }}>↑ {fmtBRL(entTotal)}</span>
                <span style={{ color: '#ff6b6b' }}>↓ {fmtBRL(saiTotal)}</span>
                <span style={{ color: lucro >= 0 ? '#34c759' : '#ff3b30', fontWeight: 700 }}>
                  {lucro >= 0 ? '+' : ''}{fmtBRL(lucro)}
                </span>
                <span style={{ color: 'var(--tx3)', textAlign: 'right' }}>{fmtBRL(r.saldoConfirmado)}</span>
                <span style={{ color: 'var(--tx3)', textAlign: 'center', fontSize: 11 }}>{isOpen ? '▲' : '▼'}</span>
              </div>

              {/* Detalhe analítico */}
              {isOpen && (
                <div style={{ padding: '10px 14px 14px', borderTop: '1px solid var(--bd)' }}>
                  {r.entradas.length > 0 && (
                    <div style={{ marginBottom: 10 }}>
                      <div style={{ fontSize: 11, fontWeight: 800, textTransform: 'uppercase', letterSpacing: '.6px', color: '#34c759', marginBottom: 6 }}>
                        Entradas
                      </div>
                      {r.entradas.map((e, i) => (
                        <div key={i} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', fontSize: 13, padding: '5px 0', borderBottom: '1px solid var(--bg)' }}>
                          <span style={{ color: 'var(--tx1)' }}>
                            {e.descricao}
                            {e.categoria && (
                              <span style={{ marginLeft: 8, fontSize: 11, color: 'var(--tx3)', background: 'rgba(52,199,89,.12)', border: '1px solid rgba(52,199,89,.25)', borderRadius: 4, padding: '1px 6px' }}>
                                {e.categoria}
                              </span>
                            )}
                          </span>
                          <span style={{ color: '#34c759', fontWeight: 600 }}>+{fmtBRL(e.valor)}</span>
                        </div>
                      ))}
                    </div>
                  )}

                  {r.saidas.length > 0 && (
                    <div style={{ marginBottom: 10 }}>
                      <div style={{ fontSize: 11, fontWeight: 800, textTransform: 'uppercase', letterSpacing: '.6px', color: '#ff6b6b', marginBottom: 6 }}>
                        Saídas
                      </div>
                      {r.saidas.map((s, i) => (
                        <div key={i} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', fontSize: 13, padding: '5px 0', borderBottom: '1px solid var(--bg)' }}>
                          <span style={{ color: 'var(--tx1)' }}>
                            {s.descricao}
                            {s.categoria && (
                              <span style={{ marginLeft: 8, fontSize: 11, color: 'var(--tx3)', background: 'rgba(255,107,107,.1)', border: '1px solid rgba(255,107,107,.25)', borderRadius: 4, padding: '1px 6px' }}>
                                {s.categoria}
                              </span>
                            )}
                          </span>
                          <span style={{ color: '#ff6b6b', fontWeight: 600 }}>-{fmtBRL(s.valor)}</span>
                        </div>
                      ))}
                    </div>
                  )}

                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 8 }}>
                    <button
                      style={{ background: 'none', border: '1px solid var(--bd)', borderRadius: 6, color: '#ff6b6b', fontSize: 12, padding: '3px 10px', cursor: 'pointer' }}
                      onClick={() => setDelData(r.data)}
                    >
                      🗑 Excluir registro
                    </button>
                    <span style={{ fontSize: 12, color: 'var(--tx3)' }}>
                      Saldo confirmado: <strong style={{ color: 'var(--tx1)' }}>{fmtBRL(r.saldoConfirmado)}</strong>
                    </span>
                  </div>
                </div>
              )}
            </div>
          )
        })}
      </div>

      <Modal
        open={!!delData}
        title={`Excluir registro de ${delData}?`}
        onClose={() => setDelData(null)}
        footer={
          <>
            <button className="btn-cancel" onClick={() => setDelData(null)}>Cancelar</button>
            <button className="btn-danger" onClick={handleDelete}>Excluir</button>
          </>
        }
      >
        {erroDelete && <div style={{ color: '#ff6b6b', fontSize: 13, marginBottom: 12, background: 'var(--err-bg)', border: '1px solid #ff3b30', padding: '8px 12px', borderRadius: 8 }}>{erroDelete}</div>}
        <div className="inp-group"><label>Motivo</label><input value={motivo} onChange={e => setMotivo(e.target.value)} placeholder="Informe o motivo" /></div>
      </Modal>
    </>
  )
}
