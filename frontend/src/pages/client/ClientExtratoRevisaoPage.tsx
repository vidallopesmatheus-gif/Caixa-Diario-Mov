import { useState, useEffect, useCallback, useMemo, useRef } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { listarPendentesCategorizacao, categorizarPendentes } from '../../api/importacao'
import { listarContasBancarias } from '../../api/contasBancarias'
import { listarCategorias } from '../../api/categorias'
import { converterLancamentoEmTransferencia } from '../../api/transferencias'
import { buscarCandidatoContrapartida } from '../../utils/candidatoTransferencia'
import { fmtBRL } from '../../utils/format'
import { useAuth } from '../../contexts/AuthContext'
import { agruparPorDescricaoSimilar } from '../../utils/descricaoSimilar'
import CategoriaCombobox from '../../components/shared/CategoriaCombobox'
import Modal from '../../components/shared/Modal'
import type { Categorias, CategoriaAdmin, PendenteCategorizacao, ContaBancaria, LancamentoExtrato } from '../../types'
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
  const [contasBancarias, setContasBancarias] = useState<ContaBancaria[]>([])
  const [nomeConta, setNomeConta] = useState('')
  const [loading, setLoading] = useState(true)
  const [salvandoIds, setSalvandoIds] = useState<Set<string>>(new Set())
  const [msg, setMsg] = useState('')
  const [selecionados, setSelecionados] = useState<Set<string>>(new Set())
  const [categoriaLote, setCategoriaLote] = useState('')

  // ── Reclassificar como Transferência (fato permutativo — não é receita nem despesa) ──────────
  const [itemParaTransferencia, setItemParaTransferencia] = useState<PendenteCategorizacao | null>(null)
  const [contaContrapartidaId, setContaContrapartidaId] = useState('')
  const [candidatoContrapartida, setCandidatoContrapartida] = useState<LancamentoExtrato | null>(null)
  const [buscandoCandidato, setBuscandoCandidato] = useState(false)
  const [convertendo, setConvertendo] = useState(false)

  // ── Mesma classificação, em lote — um par de transferência por lançamento, não um único par
  // somando os valores (extratos trazem várias aplicações/resgates repetidos pro mesmo destino).
  const [loteTransferenciaItens, setLoteTransferenciaItens] = useState<PendenteCategorizacao[] | null>(null)
  const [loteContaContrapartidaId, setLoteContaContrapartidaId] = useState('')
  const [aplicandoLoteTransferencia, setAplicandoLoteTransferencia] = useState(false)
  const [loteTransferenciaFeitos, setLoteTransferenciaFeitos] = useState(0)

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
      setContasBancarias(contas)
      setNomeConta(contas.find(c => c.id === contaId)?.nome ?? '')
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao carregar lançamentos pendentes.')
    } finally {
      setLoading(false)
    }
  }, [contaId, clienteId])

  useEffect(() => { carregar() }, [carregar])

  const grupos = useMemo(() => agruparPorDescricaoSimilar(pendentes), [pendentes])

  const tiposSelecionados = useMemo(() => {
    const tipos = new Set(pendentes.filter(p => selecionados.has(p.id)).map(p => p.tipo))
    return tipos
  }, [pendentes, selecionados])
  const loteMisto = tiposSelecionados.size > 1
  const categoriasLote = tiposSelecionados.has('Entrada') && !loteMisto ? categorias.entradas : categorias.saidas

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
    const item = { nome: nova.nome, tipoCusto: nova.tipo, grupo: nova.grupoNome }
    // Investimento/Financiamento podem ser lançados como entrada ou saída — entram nos dois lados.
    if (nova.tipo === 'Receita' || nova.tipo === 'Investimento' || nova.tipo === 'Financiamento')
      setCategorias(prev => ({ ...prev, entradas: [...prev.entradas, item] }))
    if (nova.tipo !== 'Receita')
      setCategorias(prev => ({ ...prev, saidas: [...prev.saidas, item] }))
  }

  function abrirModalTransferencia(item: PendenteCategorizacao) {
    setItemParaTransferencia(item)
    setContaContrapartidaId('')
    setCandidatoContrapartida(null)
    setMsg('')
  }

  useEffect(() => {
    if (!itemParaTransferencia || !contaContrapartidaId) { setCandidatoContrapartida(null); return }
    let cancelado = false
    setBuscandoCandidato(true)
    buscarCandidatoContrapartida(contaContrapartidaId, itemParaTransferencia.data, itemParaTransferencia.valor, itemParaTransferencia.tipo)
      .then(candidato => { if (!cancelado) setCandidatoContrapartida(candidato) })
      .finally(() => { if (!cancelado) setBuscandoCandidato(false) })
    return () => { cancelado = true }
  }, [itemParaTransferencia, contaContrapartidaId])

  async function confirmarTransferencia(vincular: boolean) {
    if (!itemParaTransferencia || !contaId || !contaContrapartidaId) return
    setConvertendo(true)
    setMsg('')
    try {
      await converterLancamentoEmTransferencia({
        contaId, lancamentoId: itemParaTransferencia.id, data: itemParaTransferencia.data,
        tipo: itemParaTransferencia.tipo, contaContrapartidaId,
        lancamentoContrapartidaId: vincular && candidatoContrapartida?.id ? candidatoContrapartida.id : undefined,
        dataContrapartida: vincular ? candidatoContrapartida?.data : undefined,
      })
      setPendentes(prev => prev.filter(p => p.id !== itemParaTransferencia.id))
      setItemParaTransferencia(null)
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao converter em transferência.')
    } finally {
      setConvertendo(false)
    }
  }

  function abrirLoteTransferencia(itens: PendenteCategorizacao[]) {
    if (itens.length === 0) return
    setLoteTransferenciaItens(itens)
    setLoteContaContrapartidaId('')
    setLoteTransferenciaFeitos(0)
    setMsg('')
  }

  async function confirmarLoteTransferencia() {
    if (!loteTransferenciaItens || !contaId || !loteContaContrapartidaId) return
    setAplicandoLoteTransferencia(true)
    setMsg('')
    // Sequencial (não Promise.all): itens do mesmo dia compartilham o mesmo RegistroDiario — duas
    // chamadas em paralelo fariam leitura-e-escrita concorrente na mesma linha e uma perderia a
    // alteração da outra. Um lançamento por vez garante que cada gravação parte do estado já salvo
    // pela anterior.
    const idsComSucesso: string[] = []
    let falhas = 0
    for (const item of loteTransferenciaItens) {
      try {
        await converterLancamentoEmTransferencia({
          contaId, lancamentoId: item.id, data: item.data, tipo: item.tipo,
          contaContrapartidaId: loteContaContrapartidaId,
        })
        idsComSucesso.push(item.id)
      } catch {
        falhas++
      }
      setLoteTransferenciaFeitos(f => f + 1)
    }

    setPendentes(prev => prev.filter(p => !idsComSucesso.includes(p.id)))
    setSelecionados(prev => {
      const next = new Set(prev)
      idsComSucesso.forEach(id => next.delete(id))
      return next
    })
    setAplicandoLoteTransferencia(false)
    setLoteTransferenciaItens(null)
    if (falhas > 0) {
      setMsg(`${idsComSucesso.length} classificado(s) como transferência — ${falhas} falharam, continuam pendentes.`)
    }
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
              disabled={loteMisto}
            >
              <option value="">Categoria para selecionadas...</option>
              {categoriasLote.map(c => <option key={c.nome} value={c.nome}>{c.nome}</option>)}
            </select>
            <button
              className="er-btn-lote"
              onClick={aplicarCategoriaLote}
              disabled={!categoriaLote || selecionados.size === 0 || loteMisto}
            >
              Aplicar a {selecionados.size} selecionada(s)
            </button>
            <button
              type="button"
              className="er-btn-toggle"
              disabled={selecionados.size === 0}
              onClick={() => abrirLoteTransferencia(pendentes.filter(p => selecionados.has(p.id)))}
              title="Marcar todas as selecionadas como transferência entre contas"
            >
              🔁 Marcar {selecionados.size} selecionada(s) como Transferência
            </button>
            {loteMisto && (
              <span className="er-msg-erro" style={{ marginLeft: 8 }}>
                Selecione só entradas ou só saídas por vez para categorizar em lote.
              </span>
            )}
          </div>

          <div className="er-lista">
            {grupos.map(g => {
              if (g.itens.length === 1) {
                const item = g.itens[0]
                const categoriasItem = item.tipo === 'Entrada' ? categorias.entradas : categorias.saidas
                return (
                  <ExtratoLinhaPendente
                    key={item.id}
                    item={item}
                    categoriasDisponiveis={categoriasItem}
                    selecionado={selecionados.has(item.id)}
                    salvando={salvandoIds.has(item.id)}
                    onToggleSelecionado={toggleSelecionado}
                    onCategorizar={cat => aplicarCategoria([item], cat)}
                    onCategoriaCriada={handleCategoriaCriada}
                    onNavigate={navegarFoco}
                    registerInputRef={registerInputRef}
                    onMarcarTransferencia={() => abrirModalTransferencia(item)}
                  />
                )
              }

              // Todo grupo tem um único tipo — agruparPorDescricaoSimilar particiona por tipo antes de comparar.
              const tipoGrupo = g.itens[0].tipo
              const categoriasGrupo = tipoGrupo === 'Entrada' ? categorias.entradas : categorias.saidas
              return (
                <div className="er-grupo" key={g.chave}>
                  <div className="er-grupo-header">
                    <span className="er-grupo-titulo">
                      {g.itens.length} lançamentos parecidos: "{g.itens[0].descricao}"
                    </span>
                    <CategoriaCombobox
                      categorias={categoriasGrupo}
                      value=""
                      onChange={cat => aplicarCategoria(g.itens, cat)}
                      onCategoriaCriada={handleCategoriaCriada}
                      blocoPadraoNovaCategoria={tipoGrupo === 'Entrada' ? 'RECEITAS OPERACIONAIS' : 'DESPESAS OPERACIONAIS'}
                      placeholder="Categorizar todo o grupo..."
                    />
                    <button
                      type="button"
                      className="er-btn-toggle"
                      onClick={() => abrirLoteTransferencia(g.itens)}
                      title="Marcar todo o grupo como transferência entre contas"
                    >
                      🔁 Todo o grupo é Transferência
                    </button>
                  </div>
                  <div className="er-grupo-itens">
                    {g.itens.map(item => (
                      <ExtratoLinhaPendente
                        key={item.id}
                        item={item}
                        categoriasDisponiveis={categoriasGrupo}
                        selecionado={selecionados.has(item.id)}
                        salvando={salvandoIds.has(item.id)}
                        onToggleSelecionado={toggleSelecionado}
                        onCategorizar={cat => aplicarCategoria([item], cat)}
                        onCategoriaCriada={handleCategoriaCriada}
                        onNavigate={navegarFoco}
                        registerInputRef={registerInputRef}
                        onMarcarTransferencia={() => abrirModalTransferencia(item)}
                      />
                    ))}
                  </div>
                </div>
              )
            })}
          </div>
        </>
      )}

      <Modal
        open={!!itemParaTransferencia}
        title="🔁 Classificar como Transferência"
        onClose={() => setItemParaTransferencia(null)}
        footer={
          candidatoContrapartida ? (
            <>
              <button className="er-btn-lote" onClick={() => setItemParaTransferencia(null)}>Cancelar</button>
              <button className="er-btn-lote" onClick={() => confirmarTransferencia(false)} disabled={convertendo}>Criar novo mesmo assim</button>
              <button className="btn-save" onClick={() => confirmarTransferencia(true)} disabled={convertendo}>
                {convertendo ? 'Vinculando...' : 'Vincular a esse lançamento'}
              </button>
            </>
          ) : (
            <>
              <button className="er-btn-lote" onClick={() => setItemParaTransferencia(null)}>Cancelar</button>
              <button className="btn-save" onClick={() => confirmarTransferencia(false)} disabled={convertendo || !contaContrapartidaId}>
                {convertendo ? 'Convertendo...' : 'Confirmar'}
              </button>
            </>
          )
        }
      >
        {itemParaTransferencia && (
          <>
            <p style={{ fontSize: 13, color: 'var(--tx3)', marginBottom: 12 }}>
              "{itemParaTransferencia.descricao}" · {fmtBRL(itemParaTransferencia.valor)} · {fmtData(itemParaTransferencia.data)}
              <br />
              {itemParaTransferencia.tipo === 'Entrada'
                ? 'Não é receita: o dinheiro veio de outra conta sua. De qual conta veio?'
                : 'Não é despesa: o dinheiro saiu desta conta e foi para outra. Pra qual conta foi?'}
            </p>
            <div className="inp-group">
              <label>{itemParaTransferencia.tipo === 'Entrada' ? 'Conta de origem' : 'Conta de destino'}</label>
              <select value={contaContrapartidaId} onChange={e => setContaContrapartidaId(e.target.value)}>
                <option value="">Selecione...</option>
                {contasBancarias.filter(c => c.ativa && c.id !== contaId).map(c => (
                  <option key={c.id} value={c.id}>{c.nome}</option>
                ))}
              </select>
            </div>
            {buscandoCandidato && <p style={{ fontSize: 12, color: 'var(--tx3)', marginTop: 8 }}>Procurando lançamento correspondente...</p>}
            {candidatoContrapartida && (
              <p style={{ fontSize: 13, color: 'var(--warning)', marginTop: 8 }}>
                Já existe um lançamento parecido nessa conta: "{candidatoContrapartida.descricao}" de {fmtBRL(Math.abs(candidatoContrapartida.valor))} em {fmtData(candidatoContrapartida.data)}.
                Vincular a ele evita duplicar a transferência.
              </p>
            )}
          </>
        )}
      </Modal>

      <Modal
        open={!!loteTransferenciaItens}
        title="🔁 Marcar como Transferência"
        onClose={() => { if (!aplicandoLoteTransferencia) setLoteTransferenciaItens(null) }}
        footer={
          <>
            <button className="er-btn-lote" onClick={() => setLoteTransferenciaItens(null)} disabled={aplicandoLoteTransferencia}>
              Cancelar
            </button>
            <button className="btn-save" onClick={confirmarLoteTransferencia} disabled={!loteContaContrapartidaId || aplicandoLoteTransferencia}>
              {aplicandoLoteTransferencia
                ? `Classificando ${loteTransferenciaFeitos}/${loteTransferenciaItens?.length ?? 0}...`
                : `Confirmar ${loteTransferenciaItens?.length ?? 0} lançamento(s)`}
            </button>
          </>
        }
      >
        {loteTransferenciaItens && (
          <>
            <p style={{ fontSize: 13, color: 'var(--tx3)', marginBottom: 12 }}>
              {loteTransferenciaItens.length} lançamento(s) selecionado(s), cada um vira sua própria
              transferência (não um par único somando os valores). Pra qual conta é a contrapartida?
            </p>
            <div className="inp-group">
              <label>Conta contrapartida</label>
              <select value={loteContaContrapartidaId} onChange={e => setLoteContaContrapartidaId(e.target.value)} disabled={aplicandoLoteTransferencia}>
                <option value="">Selecione...</option>
                {contasBancarias.filter(c => c.ativa && c.id !== contaId).map(c => (
                  <option key={c.id} value={c.id}>{c.nome}</option>
                ))}
              </select>
            </div>
          </>
        )}
      </Modal>
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
  onMarcarTransferencia: () => void
}

function ExtratoLinhaPendente({
  item, categoriasDisponiveis, selecionado, salvando,
  onToggleSelecionado, onCategorizar, onCategoriaCriada, onNavigate, registerInputRef, onMarcarTransferencia,
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
      <div className={`er-item-valor ${item.tipo === 'Entrada' ? 'val-green' : 'val-red'}`}>
        {item.tipo === 'Entrada' ? '+' : '-'}{fmtBRL(item.valor)}
      </div>
      <CategoriaCombobox
        ref={el => registerInputRef(item.id, el)}
        categorias={categoriasDisponiveis}
        value=""
        onChange={onCategorizar}
        onCategoriaCriada={onCategoriaCriada}
        blocoPadraoNovaCategoria={item.tipo === 'Entrada' ? 'RECEITAS OPERACIONAIS' : 'DESPESAS OPERACIONAIS'}
        placeholder={salvando ? 'Salvando...' : 'Categoria'}
        onNavigate={dir => onNavigate(item.id, dir)}
      />
      <button type="button" className="er-btn-toggle" title="Não é despesa — é uma transferência entre contas" onClick={onMarcarTransferencia}>
        🔁 Transferência
      </button>
    </div>
  )
}
