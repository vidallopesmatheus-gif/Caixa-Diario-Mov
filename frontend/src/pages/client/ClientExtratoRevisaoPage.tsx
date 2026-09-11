import { useState, useEffect, useCallback, useMemo, useRef } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { listarPendentesCategorizacao, categorizarPendentes } from '../../api/importacao'
import { listarContasBancarias } from '../../api/contasBancarias'
import { listarCategorias } from '../../api/categorias'
import { converterLancamentoEmTransferencia } from '../../api/transferencias'
import { criarRegra, contarCorrespondencias } from '../../api/regras'
import { gerarLinkConciliacao, listarLinksConciliacao, revogarLinkConciliacao } from '../../api/linksConciliacao'
import { buscarCandidatoContrapartida } from '../../utils/candidatoTransferencia'
import { fmtBRL } from '../../utils/format'
import { useAuth } from '../../contexts/AuthContext'
import { agruparPorDescricaoSimilar } from '../../utils/descricaoSimilar'
import CategoriaCombobox from '../../components/shared/CategoriaCombobox'
import Modal from '../../components/shared/Modal'
import type { Categorias, CategoriaAdmin, PendenteCategorizacao, ContaBancaria, LancamentoExtrato, LinkConciliacao } from '../../types'
import './ClientExtratoRevisao.css'

function fmtData(iso: string): string {
  return iso.slice(0, 10).split('-').reverse().join('/')
}

function fmtDataHora(iso: string): string {
  return new Date(iso).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })
}

function urlDoPortal(token: string): string {
  return `${window.location.origin}/portal/conciliacao/${token}`
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

  // ── Criar regra de categorização automática, a partir de um lançamento (ou grupo) sendo
  // categorizado agora — a descrição de referência é sempre a do primeiro item do grupo/seleção.
  const [regraOrigemItens, setRegraOrigemItens] = useState<PendenteCategorizacao[] | null>(null)
  const [regraAcaoTipo, setRegraAcaoTipo] = useState<'Categoria' | 'Transferencia'>('Categoria')
  const [regraCategoria, setRegraCategoria] = useState('')
  const [regraContaContrapartidaId, setRegraContaContrapartidaId] = useState('')
  const [regraContagem, setRegraContagem] = useState<number | null>(null)
  const [criandoRegra, setCriandoRegra] = useState(false)

  // ── Portal de conciliação — link público pro cliente classificar sozinho ────────────────────
  const [links, setLinks] = useState<LinkConciliacao[]>([])
  const [mostrarModalLink, setMostrarModalLink] = useState(false)
  const [gerandoLink, setGerandoLink] = useState(false)
  const [revogandoLinkId, setRevogandoLinkId] = useState<string | null>(null)
  const [linkCopiado, setLinkCopiado] = useState(false)
  const [msgLink, setMsgLink] = useState('')

  const inputRefs = useRef<Record<string, HTMLInputElement | null>>({})
  const clienteId = user?.usuarioId ?? ''

  const carregar = useCallback(async () => {
    if (!contaId || !clienteId) return
    setLoading(true)
    try {
      const [pend, cats, contas, linksCarregados] = await Promise.all([
        listarPendentesCategorizacao(contaId),
        listarCategorias(),
        listarContasBancarias(clienteId),
        listarLinksConciliacao(clienteId),
      ])
      setPendentes(pend)
      setCategorias(cats)
      setContasBancarias(contas)
      setLinks(linksCarregados)
      setNomeConta(contas.find(c => c.id === contaId)?.nome ?? '')
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao carregar lançamentos pendentes.')
    } finally {
      setLoading(false)
    }
  }, [contaId, clienteId])

  const linkAtivo = useMemo(() => links.find(l => l.status === 'Ativo'), [links])

  async function gerarLink() {
    if (!clienteId) return
    setGerandoLink(true)
    setMsgLink('')
    try {
      const novo = await gerarLinkConciliacao(clienteId)
      setLinks(prev => [novo, ...prev.map(l => (l.status === 'Ativo' ? { ...l, status: 'Revogado' as const } : l))])
      setLinkCopiado(false)
    } catch (e: unknown) {
      setMsgLink(e instanceof Error ? e.message : 'Erro ao gerar link.')
    } finally {
      setGerandoLink(false)
    }
  }

  async function revogarLink(id: string) {
    setRevogandoLinkId(id)
    setMsgLink('')
    try {
      await revogarLinkConciliacao(id)
      setLinks(prev => prev.map(l => (l.id === id ? { ...l, status: 'Revogado' as const, revogadoEm: new Date().toISOString() } : l)))
    } catch (e: unknown) {
      setMsgLink(e instanceof Error ? e.message : 'Erro ao revogar link.')
    } finally {
      setRevogandoLinkId(null)
    }
  }

  function copiarLink(token: string) {
    navigator.clipboard.writeText(urlDoPortal(token)).then(() => {
      setLinkCopiado(true)
      setTimeout(() => setLinkCopiado(false), 2000)
    })
  }

  function urlWhatsappLink(token: string) {
    const texto = `Oi! Pode classificar alguns lançamentos pendentes por aqui? ${urlDoPortal(token)}`
    return `https://wa.me/?text=${encodeURIComponent(texto)}`
  }

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

  // Seleciona só o que está VISÍVEL agora (ordemFoco vem de `grupos`, que já reflete qualquer
  // busca/filtro aplicado) — nunca os pendentes inteiros se a lista estiver filtrada.
  function selecionarTodos() {
    setSelecionados(new Set(ordemFoco))
  }

  function desmarcarTodos() {
    setSelecionados(new Set())
  }

  function aplicarCategoriaLote() {
    if (!categoriaLote || selecionados.size === 0) return
    const itens = pendentes.filter(p => selecionados.has(p.id))
    aplicarCategoria(itens, categoriaLote)
    setCategoriaLote('')
  }

  function handleCategoriaCriada(nova: CategoriaAdmin) {
    const item = { nome: nova.nome, tipoCusto: nova.tipo, grupo: nova.grupoNome }
    // ehEntrada/ehSaida vêm prontos do backend (CategoriaService) — única fonte da regra, pra não
    // duplicar o cálculo aqui de novo (foi uma cópia divergente que causou o bug de "Devoluções").
    if (nova.ehEntrada) setCategorias(prev => ({ ...prev, entradas: [...prev.entradas, item] }))
    if (nova.ehSaida) setCategorias(prev => ({ ...prev, saidas: [...prev.saidas, item] }))
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

  function abrirModalRegra(itens: PendenteCategorizacao[]) {
    if (itens.length === 0) return
    setRegraOrigemItens(itens)
    setRegraAcaoTipo('Categoria')
    setRegraCategoria('')
    setRegraContaContrapartidaId('')
    setRegraContagem(null)
    setMsg('')
  }

  // Recalcula "quantos casariam" sempre que a descrição de referência muda (troca de item/grupo) —
  // o critério (documento ou descrição exata) é derivado dela no servidor.
  useEffect(() => {
    if (!regraOrigemItens || !contaId) { setRegraContagem(null); return }
    let cancelado = false
    contarCorrespondencias(contaId, regraOrigemItens[0].tipo, regraOrigemItens[0].descricao)
      .then(qtd => { if (!cancelado) setRegraContagem(qtd) })
      .catch(() => { if (!cancelado) setRegraContagem(null) })
    return () => { cancelado = true }
  }, [regraOrigemItens, contaId])

  async function confirmarCriarRegra() {
    if (!regraOrigemItens || !contaId) return
    if (regraAcaoTipo === 'Categoria' && !regraCategoria) return
    if (regraAcaoTipo === 'Transferencia' && !regraContaContrapartidaId) return
    setCriandoRegra(true)
    setMsg('')
    try {
      await criarRegra(clienteId, {
        contaBancariaId: contaId,
        tipo: regraOrigemItens[0].tipo,
        descricaoReferencia: regraOrigemItens[0].descricao,
        acaoTipo: regraAcaoTipo,
        categoria: regraAcaoTipo === 'Categoria' ? regraCategoria : undefined,
        contaContrapartidaId: regraAcaoTipo === 'Transferencia' ? regraContaContrapartidaId : undefined,
      })
      setRegraOrigemItens(null)
      setMsg('Regra criada! Vai valer nas próximas importações desta conta — edite ou desative em Configurações → Regras.')
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : 'Erro ao criar regra.')
    } finally {
      setCriandoRegra(false)
    }
  }

  if (loading) return <p style={{ color: 'var(--tx3)' }}>Carregando...</p>

  return (
    <>
      <div className="er-header">
        <div>
          <h2 className="er-titulo">Categorizar Lançamentos</h2>
          <div className="er-subtitulo">{nomeConta} · lançamentos já importados, só falta a categoria</div>
          {linkAtivo && (
            <div className="er-link-resumo">
              📤 Link ativo até {fmtDataHora(linkAtivo.expiraEm)} · {linkAtivo.totalClassificadosPeloCliente} classificado(s) pelo cliente
            </div>
          )}
        </div>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          <button className="er-btn-toggle" onClick={() => setMostrarModalLink(true)}>
            📤 {linkAtivo ? 'Ver link enviado' : 'Enviar para o cliente'}
          </button>
          <button className="er-btn-voltar" onClick={() => navigate(`/banco/${contaId}`)}>← Voltar</button>
        </div>
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
              {selecionados.size > 0 && <> · <strong>{selecionados.size}</strong> selecionado(s)</>}
            </div>
            <div className="er-sticky-selecao">
              <button type="button" className="er-btn-toggle" onClick={selecionarTodos} disabled={selecionados.size === ordemFoco.length}>
                Selecionar todos
              </button>
              <button type="button" className="er-btn-toggle" onClick={desmarcarTodos} disabled={selecionados.size === 0}>
                Desmarcar todos
              </button>
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
                    onCriarRegra={() => abrirModalRegra([item])}
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
                    <button
                      type="button"
                      className="er-btn-toggle"
                      onClick={() => abrirModalRegra(g.itens)}
                      title="Criar regra pra categorizar automaticamente lançamentos parecidos nas próximas importações"
                    >
                      ⚙️ Criar regra pro grupo
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
                        onCriarRegra={() => abrirModalRegra([item])}
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

      <Modal
        open={!!regraOrigemItens}
        title="⚙️ Criar regra de categorização automática"
        onClose={() => setRegraOrigemItens(null)}
        footer={
          <>
            <button className="er-btn-lote" onClick={() => setRegraOrigemItens(null)} disabled={criandoRegra}>Cancelar</button>
            <button
              className="btn-save"
              onClick={confirmarCriarRegra}
              disabled={criandoRegra || (regraAcaoTipo === 'Categoria' ? !regraCategoria : !regraContaContrapartidaId)}
            >
              {criandoRegra ? 'Criando...' : 'Criar regra'}
            </button>
          </>
        }
      >
        {regraOrigemItens && (
          <>
            <p style={{ fontSize: 13, color: 'var(--tx3)', marginBottom: 12 }}>
              Baseada em "{regraOrigemItens[0].descricao}". Vai categorizar automaticamente, nas
              próximas importações desta conta, todo lançamento {regraOrigemItens[0].tipo === 'Entrada' ? 'de entrada' : 'de saída'} que
              {' '}{regraOrigemItens[0].descricao.match(/\d{2}\.?\d{3}\.?\d{3}\/?\d{4}-?\d{2}|\d{3}\.?\d{3}\.?\d{3}-?\d{2}/)
                ? 'tiver o mesmo CNPJ/CPF do favorecido' : 'tiver essa mesma descrição'}.
            </p>
            <div className="inp-group">
              <label>Ação da regra</label>
              <div style={{ display: 'flex', gap: 8 }}>
                <button
                  type="button"
                  onClick={() => setRegraAcaoTipo('Categoria')}
                  className={regraAcaoTipo === 'Categoria' ? 'btn-save' : 'er-btn-lote'}
                >
                  Categorizar
                </button>
                <button
                  type="button"
                  onClick={() => setRegraAcaoTipo('Transferencia')}
                  className={regraAcaoTipo === 'Transferencia' ? 'btn-save' : 'er-btn-lote'}
                >
                  🔁 Marcar como Transferência
                </button>
              </div>
            </div>

            {regraAcaoTipo === 'Categoria' ? (
              <div className="inp-group" style={{ marginTop: 12 }}>
                <label>Categoria</label>
                <CategoriaCombobox
                  categorias={regraOrigemItens[0].tipo === 'Entrada' ? categorias.entradas : categorias.saidas}
                  value={regraCategoria}
                  onChange={setRegraCategoria}
                  onCategoriaCriada={handleCategoriaCriada}
                  blocoPadraoNovaCategoria={regraOrigemItens[0].tipo === 'Entrada' ? 'RECEITAS OPERACIONAIS' : 'DESPESAS OPERACIONAIS'}
                />
              </div>
            ) : (
              <div className="inp-group" style={{ marginTop: 12 }}>
                <label>Conta contrapartida</label>
                <select value={regraContaContrapartidaId} onChange={e => setRegraContaContrapartidaId(e.target.value)}>
                  <option value="">Selecione...</option>
                  {contasBancarias.filter(c => c.ativa && c.id !== contaId).map(c => (
                    <option key={c.id} value={c.id}>{c.nome}</option>
                  ))}
                </select>
              </div>
            )}

            <p style={{ fontSize: 12, color: 'var(--tx3)', marginTop: 12 }}>
              {regraContagem === null
                ? 'Calculando quantos lançamentos pendentes já casam com esse critério...'
                : regraContagem === 0
                  ? 'Nenhum outro lançamento pendente casa com esse critério hoje.'
                  : `${regraContagem} lançamento(s) pendente(s) hoje casam com esse critério — só valem pra importações futuras, a menos que você aplique retroativamente depois em Configurações → Regras.`}
            </p>
          </>
        )}
      </Modal>

      <Modal
        open={mostrarModalLink}
        title="📤 Portal de Conciliação"
        onClose={() => setMostrarModalLink(false)}
        footer={<button className="er-btn-lote" onClick={() => setMostrarModalLink(false)}>Fechar</button>}
      >
        <p style={{ fontSize: 13, color: 'var(--tx3)', marginBottom: 12 }}>
          Gera um link sem login, válido por 24h, pro cliente classificar sozinho os lançamentos
          pendentes de todas as contas dele. Gerar um novo substitui o anterior.
        </p>
        {msgLink && <div className="er-msg-erro">{msgLink}</div>}

        {linkAtivo ? (
          <div className="er-link-card">
            <div className="er-link-url">{urlDoPortal(linkAtivo.token)}</div>
            <div className="er-link-acoes">
              <button className="er-btn-lote" onClick={() => copiarLink(linkAtivo.token)}>
                {linkCopiado ? '✓ Copiado' : 'Copiar link'}
              </button>
              <a className="er-btn-lote" href={urlWhatsappLink(linkAtivo.token)} target="_blank" rel="noreferrer">
                WhatsApp
              </a>
              <button
                className="er-btn-lote"
                onClick={() => revogarLink(linkAtivo.id)}
                disabled={revogandoLinkId === linkAtivo.id}
              >
                {revogandoLinkId === linkAtivo.id ? 'Revogando...' : 'Revogar'}
              </button>
            </div>
            <div className="er-link-meta">
              Expira em {fmtDataHora(linkAtivo.expiraEm)} · {linkAtivo.totalClassificadosPeloCliente} classificado(s) pelo cliente
              {linkAtivo.ultimoAcessoEm && <> · último acesso {fmtDataHora(linkAtivo.ultimoAcessoEm)}</>}
            </div>
            <button className="er-btn-toggle" style={{ marginTop: 8 }} onClick={gerarLink} disabled={gerandoLink}>
              {gerandoLink ? 'Gerando...' : 'Gerar novo link (substitui este)'}
            </button>
          </div>
        ) : (
          <button className="btn-save" onClick={gerarLink} disabled={gerandoLink}>
            {gerandoLink ? 'Gerando...' : 'Gerar link'}
          </button>
        )}

        {links.filter(l => l.status !== 'Ativo').length > 0 && (
          <div style={{ marginTop: 16 }}>
            <div style={{ fontSize: 12, fontWeight: 700, color: 'var(--tx3)', textTransform: 'uppercase', marginBottom: 6 }}>
              Histórico
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
              {links.filter(l => l.status !== 'Ativo').map(l => (
                <div key={l.id} className="er-link-historico-item">
                  <span>{l.status} · criado {fmtDataHora(l.criadoEm)}</span>
                  <span>{l.totalClassificadosPeloCliente} classificado(s)</span>
                </div>
              ))}
            </div>
          </div>
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
  onCriarRegra: () => void
}

function ExtratoLinhaPendente({
  item, categoriasDisponiveis, selecionado, salvando,
  onToggleSelecionado, onCategorizar, onCategoriaCriada, onNavigate, registerInputRef, onMarcarTransferencia, onCriarRegra,
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
      <button type="button" className="er-btn-toggle" title="Criar regra pra categorizar automaticamente lançamentos parecidos nas próximas importações" onClick={onCriarRegra}>
        ⚙️ Criar regra
      </button>
    </div>
  )
}
