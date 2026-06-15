import React, { useState, useEffect } from 'react'
import { listarAuditoria } from '../../api/auditoria'
import type { AuditLog } from '../../api/auditoria'

interface Props { clienteId: string }

export default function AdminAuditoriaPage({ clienteId }: Props) {
  const [logs, setLogs] = useState<AuditLog[]>([])
  const [total, setTotal] = useState(0)
  const [pagina, setPagina] = useState(1)
  const [entidade, setEntidade] = useState('')
  const [acao, setAcao] = useState('')
  const [expanded, setExpanded] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    if (!clienteId) return
    setLoading(true)
    listarAuditoria(clienteId, { entidade: entidade || undefined, acao: acao || undefined, pagina })
      .then(res => { setLogs(res.items); setTotal(res.total) })
      .catch(console.error)
      .finally(() => setLoading(false))
  }, [clienteId, entidade, acao, pagina])

  const totalPaginas = Math.ceil(total / 50)

  return (
    <div style={{ padding: 16 }}>
      <h3 style={{ marginBottom: 16 }}>📋 Histórico de Alterações</h3>

      <div style={{ display: 'flex', gap: 8, marginBottom: 16, flexWrap: 'wrap' }}>
        <select value={entidade} onChange={e => { setEntidade(e.target.value); setPagina(1) }}
          style={{ padding: '6px 10px', borderRadius: 6, border: '1px solid var(--bd)', background: 'var(--bg-card)', color: 'var(--tx1)' }}>
          <option value="">Todas as entidades</option>
          <option value="RegistroDiario">Registro Diário</option>
          <option value="ContaRecorrente">Conta Recorrente</option>
          <option value="MetaAnual">Meta Anual</option>
        </select>
        <select value={acao} onChange={e => { setAcao(e.target.value); setPagina(1) }}
          style={{ padding: '6px 10px', borderRadius: 6, border: '1px solid var(--bd)', background: 'var(--bg-card)', color: 'var(--tx1)' }}>
          <option value="">Todas as ações</option>
          <option value="Criacao">Criação</option>
          <option value="Edicao">Edição</option>
          <option value="Exclusao">Exclusão</option>
        </select>
      </div>

      {loading && <p style={{ color: 'var(--tx3)' }}>Carregando...</p>}

      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
        <thead>
          <tr style={{ borderBottom: '1px solid var(--bd)' }}>
            <th style={{ padding: '8px 4px', textAlign: 'left', color: 'var(--tx3)' }}>Data/Hora</th>
            <th style={{ padding: '8px 4px', textAlign: 'left', color: 'var(--tx3)' }}>Entidade</th>
            <th style={{ padding: '8px 4px', textAlign: 'left', color: 'var(--tx3)' }}>Ação</th>
            <th style={{ padding: '8px 4px', textAlign: 'left', color: 'var(--tx3)' }}>Detalhes</th>
          </tr>
        </thead>
        <tbody>
          {logs.map(log => (
            <React.Fragment key={log.id}>
              <tr style={{ borderBottom: '1px solid var(--bd)' }}>
                <td style={{ padding: '8px 4px' }}>{new Date(log.ocorridoEm).toLocaleString('pt-BR')}</td>
                <td style={{ padding: '8px 4px' }}>{log.entidade}</td>
                <td style={{ padding: '8px 4px' }}>
                  <span style={{
                    padding: '2px 8px', borderRadius: 4, fontSize: 11, fontWeight: 600,
                    background: log.acaoTipo === 'Criacao' ? '#34c75920' : log.acaoTipo === 'Exclusao' ? '#ff3b3020' : '#0a84ff20',
                    color: log.acaoTipo === 'Criacao' ? '#34c759' : log.acaoTipo === 'Exclusao' ? '#ff3b30' : '#0a84ff',
                  }}>
                    {log.acaoTipo}
                  </span>
                </td>
                <td style={{ padding: '8px 4px' }}>
                  {(log.dadosAntes || log.dadosDepois) && (
                    <button onClick={() => setExpanded(expanded === log.id ? null : log.id)}
                      style={{ fontSize: 11, background: 'none', border: '1px solid var(--bd)', borderRadius: 4, padding: '2px 8px', cursor: 'pointer', color: 'var(--tx3)' }}>
                      {expanded === log.id ? 'Fechar' : 'Ver diff'}
                    </button>
                  )}
                </td>
              </tr>
              {expanded === log.id && (
                <tr>
                  <td colSpan={4} style={{ padding: '8px 4px', background: 'var(--bg-card)' }}>
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
                      {log.dadosAntes && (
                        <div>
                          <div style={{ fontSize: 11, color: 'var(--tx3)', marginBottom: 4 }}>Antes:</div>
                          <pre style={{ fontSize: 11, overflow: 'auto', maxHeight: 200, padding: 8, background: '#ff3b3010', borderRadius: 4 }}>
                            {(() => { try { return JSON.stringify(JSON.parse(log.dadosAntes!), null, 2) } catch { return log.dadosAntes } })()}
                          </pre>
                        </div>
                      )}
                      {log.dadosDepois && (
                        <div>
                          <div style={{ fontSize: 11, color: 'var(--tx3)', marginBottom: 4 }}>Depois:</div>
                          <pre style={{ fontSize: 11, overflow: 'auto', maxHeight: 200, padding: 8, background: '#34c75910', borderRadius: 4 }}>
                            {(() => { try { return JSON.stringify(JSON.parse(log.dadosDepois!), null, 2) } catch { return log.dadosDepois } })()}
                          </pre>
                        </div>
                      )}
                    </div>
                  </td>
                </tr>
              )}
            </React.Fragment>
          ))}
        </tbody>
      </table>

      {totalPaginas > 1 && (
        <div style={{ display: 'flex', gap: 8, marginTop: 16, justifyContent: 'center' }}>
          <button onClick={() => setPagina(p => Math.max(1, p - 1))} disabled={pagina === 1}
            style={{ padding: '6px 14px', borderRadius: 6, border: '1px solid var(--bd)', background: 'var(--bg-card)', color: 'var(--tx1)', cursor: 'pointer' }}>
            ← Anterior
          </button>
          <span style={{ padding: '6px 14px', color: 'var(--tx3)' }}>{pagina} / {totalPaginas}</span>
          <button onClick={() => setPagina(p => Math.min(totalPaginas, p + 1))} disabled={pagina === totalPaginas}
            style={{ padding: '6px 14px', borderRadius: 6, border: '1px solid var(--bd)', background: 'var(--bg-card)', color: 'var(--tx1)', cursor: 'pointer' }}>
            Próxima →
          </button>
        </div>
      )}
    </div>
  )
}
