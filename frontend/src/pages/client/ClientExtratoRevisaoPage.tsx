import { useState, useEffect, useCallback, useMemo, useRef } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { listarPendentesCategorizacao, categorizarPendentes } from '../../api/importacao'
import { listarContasBancarias } from '../../api/contasBancarias'
import { listarCategorias } from '../../api/categorias'
import { fmtBRL } from '../../utils/format'
import { useAuth } from '../../contexts/AuthContext'
import { agruparPorDescricaoSimilar } from '../../utils/descricaoSimilar'
import CategoriaCombobox from '../../components/shared/CategoriaCombobox'
import type { Categorias, CategoriaAdmin, PendenteCategorizacao } from '../../types'
import './ClientExtratoRevisao.css'

function fmtData(iso: string): string {
  return iso.slice(0, 10).split('-').reverse().join('/')
}

export default function ClientExtratoRevisaoPage() {
  const { contaId } = useParams<{ contaId: string }>()
  const { user } = useAuth()
  const navigate = useNavigate()

  const [pendentes, setPendentes] = useState<PendenteCategorizacao[]>([])
  const [categorias, setCategorias] = useState<Categorias>({ entradas: [], saidas: [] })
  const [nomeConta, setNomeConta] = useState('')
  const [loading, setLoading] = useState(true)
  const [salvandoIds, setSalvandoIds] = useState<Set<string>>(new Set())
  const [msg, setMsg] = useState('')
  const [selecionados, setSelecionados] = useState<Set<string>>(new Set())
  const [categoriaLote, setCategoriaLote] = useState('')

  const inputRefs = useRef<Record<string, HTMLInputElement | null>>({})
  const clienteId = user?.usuarioId ?? ''

  const carregar = useCallback(async () => {
    if (!contaId || !clienteId) return
    setLoading(true)
    try {
      const [pend, cats, contas] = await Promise.all([
        listarPendentesCategorizacao(contaId),
        listarCategorias(),
        listarContasBancarias(clienteId),
      ])
      setPendentes(pend)
      setCategorias(cats)
      setNomeConta(contas.find(c => c.id === contaId)?.nome ?? '')
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao carregar lançamentos pendentes.')
    } finally {
      setLoading(false)
    }
  }, [contaId, clienteId])

  useEffect(() => { carregar() }, [carregar])

  const grupos = useMemo(() => agruparPorDescricaoSimilar(pendentes), [pendentes])

  const ordemFoco = useMemo(() => grupos.flatMap(g => g.itens.map(i => i.id)), [grupos])

  function registerInputRef(id: string, el: HTMLInputElement | null) {
    inputRefs.current[id] = el
  }

  function navegarFoco(id: string, direcao: 'up' | 'down') {
    const idx = ordemFoco.indexOf(id)
    if (idx === -1) return
    const novoId = ordemFoco[direcao === 'down' ? idx + 1 : idx - 1]
    if (novoId) inputRefs.current[novoId]?.focus()
  }

  async function aplicarCategoria(itens: PendenteCategorizacao[], categoria: string) {
    if (!contaId || itens.length === 0) return
    setSalvandoIds(prev => new Set([...prev, ...itens.map(i => i.id)]))
    setMsg('')
    try {
      await categorizarPendentes(contaId, itens.map(i => ({ id: i.id, data: i.data, categoria })))
      const idsAplicados = new Set(itens.map(i => i.id))
      setPendentes(prev => prev.filter(p => !idsAplicados.has(p.id)))
      setSelecionados(prev => {
        const next = new Set(prev)
        idsAplicados.forEach(id => next.delete(id))
        return next
      })
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao salvar categoria.')
    } finally {
      setSalvandoIds(prev => {
        const next = new Set(prev)
        itens.forEach(i => next.delete(i.id))
        return next
      })
    }
  }

  function toggleSelecionado(id: string) {
    setSelecionados(prev => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  function aplicarCategoriaLote() {
    if (!categoriaLote || selecionados.size === 0) return
    const itens = pendentes.filter(p => selecionados.has(p.id))
    aplicarCategoria(itens, categoriaLote)
    setCategoriaLote('')
  }

  function handleCategoriaCriada(nova: CategoriaAdmin) {
    setCategorias(prev => ({ ...prev, saidas: [...prev.saidas, { nome: nova.nome, tipoCusto: nova.tipo, grupo: nova.grupo }] }))
  }

  if (loading) return <p style={{ color: 'var(--tx3)' }}>Carregando...</p>

  return (
    <>
      <div className="er-header">
        <div>
          <h2 className="er-titulo">Categorizar Lançamentos</h2>
          <div className="er-subtitulo">{nomeConta} · lançamentos já importados, só falta a categoria</div>
        </div>
        <button className="er-btn-voltar" onClick={() => navigate(`/banco/${contaId}`)}>← Voltar</button>
      </div>

      {pendentes.length === 0 ? (
        <div className="er-vazio">
          <p>Nenhum lançamento pendente de categorização nesta conta. 🎉</p>
          <button className="btn-save" onClick={() => navigate(`/banco/${contaId}`)}>
            ← Voltar para a conta
          </button>
        </div>
      ) : (
        <>
          <div className="er-sticky-bar">
            <div className="er-sticky-contador">
              <strong>{pendentes.length}</strong> lançamento(s) aguardando categoria
            </div>
          </div>

          {msg && <div className="er-msg-erro">{msg}</div>}

          <div className="er-acoes-lote">
            <select
              className="er-cat-select"
              value={categoriaLote}
              onChange={e => setCategoriaLote(e.target.value)}
            >
              <option value="">Categoria para selecionadas...</option>
              {categorias.saidas.map(c => <option key={c.nome} value={c.nome}>{c.nome}</option>)}
            </select>
            <button
              className="er-btn-lote"
              onClick={aplicarCategoriaLote}
              disabled={!categoriaLote || selecionados.size === 0}
            >
              Aplicar a {selecionados.size} selecionada(s)
            </button>
          </div>

          <div className="er-lista">
            {grupos.map(g => {
              if (g.itens.length === 1) {
                const item = g.itens[0]
                return (
                  <ExtratoLinhaPendente
                    key={item.id}
                    item={item}
                    categoriasDisponiveis={categorias.saidas}
                    selecionado={selecionados.has(item.id)}
                    salvando={salvandoIds.has(item.id)}
                    onToggleSelecionado={toggleSelecionado}
                    onCategorizar={cat => aplicarCategoria([item], cat)}
                    onCategoriaCriada={handleCategoriaCriada}
                    onNavigate={navegarFoco}
                    registerInputRef={registerInputRef}
                  />
                )
              }

              return (
                <div className="er-grupo" key={g.chave}>
                  <div className="er-grupo-header">
                    <span className="er-grupo-titulo">
                      {g.itens.length} lançamentos parecidos: "{g.itens[0].descricao}"
                    </span>
                    <CategoriaCombobox
                      categorias={categorias.saidas}
                      value=""
                      onChange={cat => aplicarCategoria(g.itens, cat)}
                      onCategoriaCriada={handleCategoriaCriada}
                      tipoPadraoNovaCategoria="CustoVariavel"
                      placeholder="Categorizar todo o grupo..."
                    />
                  </div>
                  <div className="er-grupo-itens">
                    {g.itens.map(item => (
                      <ExtratoLinhaPendente
                        key={item.id}
                        item={item}
                        categoriasDisponiveis={categorias.saidas}
                        selecionado={selecionados.has(item.id)}
                        salvando={salvandoIds.has(item.id)}
                        onToggleSelecionado={toggleSelecionado}
                        onCategorizar={cat => aplicarCategoria([item], cat)}
                        onCategoriaCriada={handleCategoriaCriada}
                        onNavigate={navegarFoco}
                        registerInputRef={registerInputRef}
                      />
                    ))}
                  </div>
                </div>
              )
            })}
          </div>
        </>
      )}
    </>
  )
}

interface ExtratoLinhaPendenteProps {
  item: PendenteCategorizacao
  categoriasDisponiveis: Categorias['saidas']
  selecionado: boolean
  salvando: boolean
  onToggleSelecionado: (id: string) => void
  onCategorizar: (categoria: string) => void
  onCategoriaCriada: (categoria: CategoriaAdmin) => void
  onNavigate: (id: string, direcao: 'up' | 'down') => void
  registerInputRef: (id: string, el: HTMLInputElement | null) => void
}

function ExtratoLinhaPendente({
  item, categoriasDisponiveis, selecionado, salvando,
  onToggleSelecionado, onCategorizar, onCategoriaCriada, onNavigate, registerInputRef,
}: ExtratoLinhaPendenteProps) {
  return (
    <div className="er-item">
      <input
        type="checkbox"
        checked={selecionado}
        onChange={() => onToggleSelecionado(item.id)}
        title="Selecionar para categorização em lote"
      />
      <div className="er-item-data">{fmtData(item.data)}</div>
      <div className="er-item-info">
        <div className="er-item-desc">{item.descricao}</div>
      </div>
      <div className="er-item-valor val-red">-{fmtBRL(item.valor)}</div>
      <CategoriaCombobox
        ref={el => registerInputRef(item.id, el)}
        categorias={categoriasDisponiveis}
        value=""
        onChange={onCategorizar}
        onCategoriaCriada={onCategoriaCriada}
        tipoPadraoNovaCategoria="CustoVariavel"
        placeholder={salvando ? 'Salvando...' : 'Categoria'}
        onNavigate={dir => onNavigate(item.id, dir)}
      />
    </div>
  )
}
