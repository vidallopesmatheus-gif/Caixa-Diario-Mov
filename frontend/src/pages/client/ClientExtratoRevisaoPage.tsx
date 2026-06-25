import { useState, useEffect, useCallback } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { listarPendentes, confirmarTransacoes } from '../../api/importacao'
import { listarContasBancarias } from '../../api/contasBancarias'
import { listarCategorias } from '../../api/categorias'
import { fmtBRL } from '../../utils/format'
import { useAuth } from '../../contexts/AuthContext'
import type { TransacaoImportada, Categorias } from '../../types'
import './ClientExtratoRevisao.css'

type EstadoLocal = {
  categoria: string
  ignorar: boolean
}

export default function ClientExtratoRevisaoPage() {
  const { contaId } = useParams<{ contaId: string }>()
  const { user } = useAuth()
  const navigate = useNavigate()

  const [transacoes, setTransacoes] = useState<TransacaoImportada[]>([])
  const [estados, setEstados] = useState<Record<string, EstadoLocal>>({})
  const [categorias, setCategorias] = useState<Categorias>({ entradas: [], saidas: [] })
  const [nomeConta, setNomeConta] = useState('')
  const [loading, setLoading] = useState(true)
  const [salvando, setSalvando] = useState(false)
  const [msg, setMsg] = useState('')

  const clienteId = user?.usuarioId ?? ''

  const carregar = useCallback(async () => {
    if (!contaId || !clienteId) return
    setLoading(true)
    try {
      const [ts, cats, contas] = await Promise.all([
        listarPendentes(contaId),
        listarCategorias(),
        listarContasBancarias(clienteId),
      ])
      setTransacoes(ts)
      setCategorias(cats)
      const conta = contas.find(c => c.id === contaId)
      setNomeConta(conta?.nome ?? '')

      // Estado inicial: nenhum ignorado, categoria vazia
      const est: Record<string, EstadoLocal> = {}
      ts.forEach(t => { est[t.id] = { categoria: t.categoria ?? '', ignorar: false } })
      setEstados(est)
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao carregar transações.')
    } finally {
      setLoading(false)
    }
  }, [contaId, clienteId])

  useEffect(() => { carregar() }, [carregar])

  function toggleIgnorar(id: string) {
    setEstados(prev => ({ ...prev, [id]: { ...prev[id], ignorar: !prev[id].ignorar } }))
  }

  function setCategoria(id: string, cat: string) {
    setEstados(prev => ({ ...prev, [id]: { ...prev[id], categoria: cat } }))
  }

  function selecionarTodas(ignorar: boolean) {
    setEstados(prev => {
      const next = { ...prev }
      transacoes.forEach(t => { next[t.id] = { ...next[t.id], ignorar } })
      return next
    })
  }

  async function handleConfirmar() {
    if (!contaId) return
    const saidasSemCategoria = transacoes.filter(t =>
      t.tipo === 'Saida' && !estados[t.id]?.ignorar && !estados[t.id]?.categoria
    )
    if (saidasSemCategoria.length > 0) {
      setMsg(`${saidasSemCategoria.length} saída(s) sem categoria. Preencha ou marque como ignorar.`)
      return
    }
    setSalvando(true)
    setMsg('')
    try {
      const payload = transacoes.map(t => ({
        id: t.id,
        categoria: estados[t.id]?.categoria || undefined,
        ignorar: estados[t.id]?.ignorar ?? false,
      }))
      await confirmarTransacoes(contaId, payload)
      navigate('/contas-bancarias')
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao confirmar.')
    } finally {
      setSalvando(false)
    }
  }

  const pendentes = transacoes.filter(t => !estados[t.id]?.ignorar)
  const totalEntradas = pendentes.filter(t => t.tipo === 'Entrada').reduce((s, t) => s + t.valor, 0)
  const totalSaidas = pendentes.filter(t => t.tipo === 'Saida').reduce((s, t) => s + t.valor, 0)

  if (loading) return <p style={{ color: 'var(--tx3)' }}>Carregando...</p>

  if (transacoes.length === 0) {
    return (
      <div className="er-vazio">
        <p>Nenhuma transação pendente para esta conta.</p>
        <button className="btn-save" onClick={() => navigate('/contas-bancarias')}>
          ← Voltar para Contas
        </button>
      </div>
    )
  }

  return (
    <>
      <div className="er-header">
        <div>
          <h2 className="er-titulo">Revisão de Extrato</h2>
          <div className="er-subtitulo">{nomeConta} · {transacoes.length} transação(ões) importada(s)</div>
        </div>
        <button className="er-btn-voltar" onClick={() => navigate('/contas-bancarias')}>← Voltar</button>
      </div>

      {/* Resumo */}
      <div className="er-resumo">
        <div className="er-resumo-item">
          <span className="er-resumo-label">A confirmar</span>
          <span className="er-resumo-val">{pendentes.length}</span>
        </div>
        <div className="er-resumo-item">
          <span className="er-resumo-label">Entradas</span>
          <span className="er-resumo-val val-green">{fmtBRL(totalEntradas)}</span>
        </div>
        <div className="er-resumo-item">
          <span className="er-resumo-label">Saídas</span>
          <span className="er-resumo-val val-red">{fmtBRL(totalSaidas)}</span>
        </div>
      </div>

      {/* Ações em lote */}
      <div className="er-acoes-lote">
        <button className="er-btn-lote" onClick={() => selecionarTodas(false)}>✔ Confirmar todas</button>
        <button className="er-btn-lote er-btn-lote-ignore" onClick={() => selecionarTodas(true)}>✕ Ignorar todas</button>
      </div>

      {/* Lista de transações */}
      <div className="er-lista">
        {transacoes.map(t => {
          const est = estados[t.id] ?? { categoria: '', ignorar: false }
          const catOpts = t.tipo === 'Entrada' ? categorias.entradas : categorias.saidas
          return (
            <div key={t.id} className={`er-item ${est.ignorar ? 'er-ignorada' : ''} ${t.duplicada ? 'er-duplicada' : ''}`}>
              <div className="er-item-data">{t.data.slice(0, 10).split('-').reverse().join('/')}</div>
              <div className="er-item-info">
                <div className="er-item-desc">{t.descricao}</div>
                {t.duplicada && (
                  <div className="er-aviso-dup">⚠️ Possível duplicata já existente nos lançamentos</div>
                )}
              </div>
              <div className={`er-item-valor ${t.tipo === 'Entrada' ? 'val-green' : 'val-red'}`}>
                {t.tipo === 'Entrada' ? '+' : '-'}{fmtBRL(t.valor)}
              </div>
              {!est.ignorar && (
                <select
                  className="er-cat-select"
                  value={est.categoria}
                  onChange={e => setCategoria(t.id, e.target.value)}
                >
                  <option value="">Categoria{t.tipo === 'Saida' ? ' *' : ''}</option>
                  {catOpts.map(c => (
                    <option key={c.nome} value={c.nome}>{c.nome}</option>
                  ))}
                </select>
              )}
              <button
                className={`er-btn-toggle ${est.ignorar ? 'er-btn-toggle-on' : ''}`}
                onClick={() => toggleIgnorar(t.id)}
              >
                {est.ignorar ? 'Ignorada' : 'Ignorar'}
              </button>
            </div>
          )
        })}
      </div>

      {msg && (
        <div style={{ margin: '12px 0', color: '#ff6b6b', fontWeight: 600, fontSize: 13 }}>{msg}</div>
      )}

      <button className="btn-save" onClick={handleConfirmar} disabled={salvando}>
        {salvando ? 'Confirmando...' : `☁️ Confirmar ${pendentes.length} transação(ões)`}
      </button>
    </>
  )
}
