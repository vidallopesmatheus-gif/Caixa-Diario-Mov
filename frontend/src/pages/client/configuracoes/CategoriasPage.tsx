import { useState, useEffect, useMemo } from 'react'
import {
  listarCategoriasParaGerenciar,
  criarCategoria,
  atualizarCategoria,
  desativarCategoria,
  reordenarCategorias,
  excluirCategoria,
  migrarCategoria,
  listarGrupos,
  criarGrupo,
  atualizarGrupo,
  desativarGrupo,
} from '../../../api/categorias'
import Modal from '../../../components/shared/Modal'
import type { CategoriaAdmin, Grupo, Bloco } from '../../../types'
import { BLOCOS_ORDEM } from '../../../types'
import '../ClientContasBancarias.css'
import './Categorias.css'

const BLOCO_LABEL: Record<Bloco, string> = {
  'RECEITAS OPERACIONAIS': 'Receitas Operacionais',
  'DEDUÇÕES DA RECEITA': 'Deduções da Receita',
  'CUSTOS OPERACIONAIS': 'Custos Operacionais',
  'DESPESAS OPERACIONAIS': 'Despesas Operacionais',
  'ATIVIDADES DE INVESTIMENTO': 'Atividades de Investimento',
  'ATIVIDADES DE FINANCIAMENTO': 'Atividades de Financiamento',
}

function lerSetSalvo<T extends string>(chave: string): Set<T> {
  try {
    const bruto = sessionStorage.getItem(chave)
    return bruto ? new Set(JSON.parse(bruto) as T[]) : new Set()
  } catch {
    return new Set()
  }
}

/** ↓ entrada, ↑ saída, ↕ os dois — ehEntrada/ehSaida vêm prontos do backend (CategoriaService). */
function DirecaoIcone({ ehEntrada, ehSaida }: { ehEntrada: boolean; ehSaida: boolean }) {
  if (ehEntrada && ehSaida) return <span className="cat-direcao cat-direcao-ambos" title="Entrada e saída">↕</span>
  if (ehEntrada) return <span className="cat-direcao cat-direcao-entrada" title="Entrada">↓</span>
  return <span className="cat-direcao cat-direcao-saida" title="Saída">↑</span>
}

export default function CategoriasPage() {
  const [categorias, setCategorias] = useState<CategoriaAdmin[]>([])
  const [grupos, setGrupos] = useState<Grupo[]>([])
  const [loading, setLoading] = useState(true)
  const [msg, setMsg] = useState('')
  const [msgOk, setMsgOk] = useState(true)

  const [novoNome, setNovoNome] = useState('')
  const [novoBloco, setNovoBloco] = useState<Bloco>('RECEITAS OPERACIONAIS')
  // '' = nada carregado ainda, '__novo__' = criar grupo novo neste bloco, senão id de um grupo existente.
  const [novoGrupoId, setNovoGrupoId] = useState('')
  const [novoGrupoNovoNome, setNovoGrupoNovoNome] = useState('')
  const [criando, setCriando] = useState(false)

  const [editId, setEditId] = useState<string | null>(null)
  const [editNome, setEditNome] = useState('')
  const [editGrupoId, setEditGrupoId] = useState('')
  const [salvandoEdit, setSalvandoEdit] = useState(false)

  const [emUso, setEmUso] = useState<{ categoria: CategoriaAdmin; quantidade: number } | null>(null)
  const [destinoMigracao, setDestinoMigracao] = useState('')
  const [migrando, setMigrando] = useState(false)

  const [busca, setBusca] = useState('')
  // sessionStorage (não localStorage) — some ao fechar a aba, mas sobrevive a navegar entre as
  // sub-abas de Configurações, que desmonta este componente (é uma <Route>, não um tab escondido).
  const [blocosColapsados, setBlocosColapsados] = useState<Set<Bloco>>(() => lerSetSalvo('planoContas.blocosColapsados'))
  const [gruposColapsados, setGruposColapsados] = useState<Set<string>>(() => lerSetSalvo('planoContas.gruposColapsados'))

  const [salvandoGrupo, setSalvandoGrupo] = useState(false)
  const [editGrupo, setEditGrupo] = useState<Grupo | null>(null)
  const [editGrupoNome, setEditGrupoNome] = useState('')
  const [editGrupoBloco, setEditGrupoBloco] = useState<Bloco>('DESPESAS OPERACIONAIS')

  useEffect(() => {
    sessionStorage.setItem('planoContas.blocosColapsados', JSON.stringify([...blocosColapsados]))
  }, [blocosColapsados])

  useEffect(() => {
    sessionStorage.setItem('planoContas.gruposColapsados', JSON.stringify([...gruposColapsados]))
  }, [gruposColapsados])

  function toggleBloco(bloco: Bloco) {
    setBlocosColapsados(prev => {
      const next = new Set(prev)
      if (next.has(bloco)) next.delete(bloco)
      else next.add(bloco)
      return next
    })
  }

  function toggleGrupo(id: string) {
    setGruposColapsados(prev => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  useEffect(() => { carregar() }, [])

  function carregar() {
    setLoading(true)
    Promise.all([listarCategoriasParaGerenciar(), listarGrupos()])
      .then(([cats, gps]) => {
        setCategorias(cats)
        setGrupos(gps)
        if (!novoGrupoId) {
          const primeiroAtivo = gps.filter(g => g.ativo).sort((a, b) => a.ordem - b.ordem)[0]
          setNovoBloco(primeiroAtivo?.bloco ?? 'RECEITAS OPERACIONAIS')
          setNovoGrupoId(primeiroAtivo?.id ?? '__novo__')
        }
      })
      .catch(() => showMsg('Erro ao carregar o Plano de Contas.', false))
      .finally(() => setLoading(false))
  }

  function showMsg(texto: string, ok = true) {
    setMsgOk(ok)
    setMsg(texto)
    setTimeout(() => setMsg(''), 3000)
  }

  const gruposAtivos = useMemo(() => grupos.filter(g => g.ativo).sort((a, b) => a.ordem - b.ordem), [grupos])

  const ativas = useMemo(() => categorias.filter(c => c.ativa).sort((a, b) => a.ordem - b.ordem), [categorias])
  const inativas = useMemo(() => categorias.filter(c => !c.ativa), [categorias])
  const indicePorId = useMemo(() => new Map(ativas.map((c, i) => [c.id, i])), [ativas])

  const buscaNormalizada = busca.trim().toLowerCase()
  // Busca por nome de categoria OU do grupo que a contém — digitar "Devolução" encontra as
  // categorias dentro do grupo "Devolução e Estorno", mesmo que nenhuma delas se chame assim.
  const gruposComNomeCorrespondente = useMemo(
    () => new Set(buscaNormalizada ? gruposAtivos.filter(g => g.nome.toLowerCase().includes(buscaNormalizada)).map(g => g.id) : []),
    [gruposAtivos, buscaNormalizada]
  )
  const categoriaCorresponde = (c: CategoriaAdmin) =>
    c.nome.toLowerCase().includes(buscaNormalizada) || gruposComNomeCorrespondente.has(c.grupoId)
  const ativasFiltradas = useMemo(
    () => buscaNormalizada ? ativas.filter(categoriaCorresponde) : ativas,
    [ativas, buscaNormalizada, gruposComNomeCorrespondente]
  )
  const inativasFiltradas = useMemo(
    () => buscaNormalizada ? inativas.filter(categoriaCorresponde) : inativas,
    [inativas, buscaNormalizada, gruposComNomeCorrespondente]
  )

  const gruposDoBlocoEscolhido = useMemo(() => gruposAtivos.filter(g => g.bloco === novoBloco), [gruposAtivos, novoBloco])

  // Árvore Bloco → Grupo → Categoria. Grupo sem categoria ainda aparece (ex.: grupos novos, vazios).
  const arvore = useMemo(() => {
    return BLOCOS_ORDEM
      .map(bloco => {
        const gruposDoBloco = gruposAtivos.filter(g => g.bloco === bloco)
        const gruposComItens = gruposDoBloco
          .map(grupo => ({ grupo, categorias: ativasFiltradas.filter(c => c.grupoId === grupo.id) }))
          .filter(g => g.categorias.length > 0 || !buscaNormalizada)
        return { bloco, grupos: gruposComItens }
      })
      .filter(b => b.grupos.length > 0)
  }, [gruposAtivos, ativasFiltradas, buscaNormalizada])

  async function handleCriar() {
    if (!novoNome.trim() || !novoGrupoId) return
    if (novoGrupoId === '__novo__' && !novoGrupoNovoNome.trim()) return
    setCriando(true)
    try {
      let grupoId = novoGrupoId
      if (grupoId === '__novo__') {
        const grupoCriado = await criarGrupo(novoGrupoNovoNome.trim(), novoBloco)
        grupoId = grupoCriado.id
      }
      await criarCategoria(novoNome.trim(), grupoId)
      setNovoNome('')
      setNovoGrupoNovoNome('')
      showMsg('Categoria criada com sucesso!')
      carregar()
    } catch (e: unknown) {
      showMsg(e instanceof Error ? e.message : 'Erro ao criar categoria.', false)
    } finally {
      setCriando(false)
    }
  }

  function iniciarEdicao(c: CategoriaAdmin) {
    setEditId(c.id)
    setEditNome(c.nome)
    setEditGrupoId(c.grupoId)
  }

  async function handleSalvarEdit() {
    if (!editId || !editGrupoId) return
    setSalvandoEdit(true)
    try {
      await atualizarCategoria(editId, editNome.trim(), editGrupoId, true)
      setEditId(null)
      showMsg('Categoria atualizada!')
      carregar()
    } catch (e: unknown) {
      showMsg(e instanceof Error ? e.message : 'Erro ao atualizar.', false)
    } finally {
      setSalvandoEdit(false)
    }
  }

  async function handleDesativar(id: string) {
    if (!confirm('Desativar esta categoria? Ela deixa de aparecer nos formulários, mas o histórico é preservado.')) return
    try {
      await desativarCategoria(id)
      showMsg('Categoria desativada.')
      carregar()
    } catch (e: unknown) {
      showMsg(e instanceof Error ? e.message : 'Erro ao desativar.', false)
    }
  }

  async function handleReativar(c: CategoriaAdmin) {
    try {
      await atualizarCategoria(c.id, c.nome, c.grupoId, true)
      showMsg('Categoria reativada.')
      carregar()
    } catch (e: unknown) {
      showMsg(e instanceof Error ? e.message : 'Erro ao reativar.', false)
    }
  }

  async function handleExcluir(c: CategoriaAdmin) {
    if (!confirm(`Excluir a categoria "${c.nome}"?`)) return
    try {
      const resultado = await excluirCategoria(c.id)
      if (!resultado.excluida) {
        setEmUso({ categoria: c, quantidade: resultado.quantidadeLancamentos })
        return
      }
      showMsg('Categoria excluída.')
      carregar()
    } catch (e: unknown) {
      showMsg(e instanceof Error ? e.message : 'Erro ao excluir.', false)
    }
  }

  async function handleMover(id: string, direcao: -1 | 1) {
    const indice = ativas.findIndex(c => c.id === id)
    const alvo = indice + direcao
    if (indice < 0 || alvo < 0 || alvo >= ativas.length) return

    const novaOrdemAtivas = [...ativas]
    const tmp = novaOrdemAtivas[indice]
    novaOrdemAtivas[indice] = novaOrdemAtivas[alvo]
    novaOrdemAtivas[alvo] = tmp

    const ids = [...novaOrdemAtivas.map(c => c.id), ...inativas.map(c => c.id)]
    try {
      await reordenarCategorias(ids)
      carregar()
    } catch (e: unknown) {
      showMsg(e instanceof Error ? e.message : 'Erro ao reordenar.', false)
    }
  }

  async function handleMigrar() {
    if (!emUso || !destinoMigracao) return
    setMigrando(true)
    try {
      await migrarCategoria(emUso.categoria.id, destinoMigracao)
      showMsg(`Lançamentos migrados para a nova categoria. "${emUso.categoria.nome}" foi desativada.`)
      setEmUso(null)
      setDestinoMigracao('')
      carregar()
    } catch (e: unknown) {
      showMsg(e instanceof Error ? e.message : 'Erro ao migrar lançamentos.', false)
    } finally {
      setMigrando(false)
    }
  }

  async function handleDesativarAoInves() {
    if (!emUso) return
    await handleDesativar(emUso.categoria.id)
    setEmUso(null)
  }

  // Migração só faz sentido entre categorias do mesmo grupo — mesma natureza contábil (Tipo/Bloco).
  const opcoesMigracao = emUso
    ? ativas.filter(c => c.id !== emUso.categoria.id && c.grupoId === emUso.categoria.grupoId)
    : []

  function iniciarEdicaoGrupo(g: Grupo) {
    setEditGrupo(g)
    setEditGrupoNome(g.nome)
    setEditGrupoBloco(g.bloco)
  }

  async function handleSalvarEdicaoGrupo() {
    if (!editGrupo || !editGrupoNome.trim()) return
    const mudouBloco = editGrupo.bloco !== editGrupoBloco
    if (mudouBloco && editGrupo.quantidadeCategorias > 0
      && !confirm(`Mudar o bloco deste grupo também muda a classificação de ${editGrupo.quantidadeCategorias} categoria(s) já cadastrada(s) nele. Continuar?`)) {
      return
    }
    setSalvandoGrupo(true)
    try {
      await atualizarGrupo(editGrupo.id, editGrupoNome.trim(), editGrupoBloco, true)
      setEditGrupo(null)
      showMsg('Grupo atualizado!')
      carregar()
    } catch (e: unknown) {
      showMsg(e instanceof Error ? e.message : 'Erro ao atualizar grupo.', false)
    } finally {
      setSalvandoGrupo(false)
    }
  }

  async function handleDesativarGrupo(g: Grupo) {
    if (g.quantidadeCategorias > 0) {
      showMsg(`"${g.nome}" tem ${g.quantidadeCategorias} categoria(s) — mova-as pra outro grupo antes de desativar.`, false)
      return
    }
    if (!confirm(`Desativar o grupo "${g.nome}"?`)) return
    try {
      await desativarGrupo(g.id)
      showMsg('Grupo desativado.')
      carregar()
    } catch (e: unknown) {
      showMsg(e instanceof Error ? e.message : 'Erro ao desativar grupo.', false)
    }
  }

  function renderCategoriaAtiva(c: CategoriaAdmin) {
    const i = indicePorId.get(c.id) ?? 0
    if (editId === c.id) {
      return (
        <div key={c.id} className="cat-item-compacta cat-edit-form">
          <input value={editNome} onChange={e => setEditNome(e.target.value)} style={{ flex: 2 }} />
          <select value={editGrupoId} onChange={e => setEditGrupoId(e.target.value)} style={{ flex: 1, minWidth: 220 }}>
            {gruposAtivos.map(g => <option key={g.id} value={g.id}>{BLOCO_LABEL[g.bloco]} · {g.nome}</option>)}
          </select>
          <button className="btn-add-conta" onClick={handleSalvarEdit} disabled={salvandoEdit}>
            {salvandoEdit ? 'Salvando...' : '✔ Salvar'}
          </button>
          <button onClick={() => setEditId(null)} className="cat-btn-cancelar">Cancelar</button>
        </div>
      )
    }
    return (
      <div key={c.id} className="cat-item-compacta">
        <div className="cat-ordem-setas">
          <button disabled={i === 0} onClick={() => handleMover(c.id, -1)} title="Mover para cima">▲</button>
          <button disabled={i === ativas.length - 1} onClick={() => handleMover(c.id, 1)} title="Mover para baixo">▼</button>
        </div>
        <DirecaoIcone ehEntrada={c.ehEntrada} ehSaida={c.ehSaida} />
        <span className="cat-nome-compacta">{c.nome}</span>
        <div className="cat-acoes-compactas">
          <button className="cb-btn-editar" onClick={() => iniciarEdicao(c)}>Editar</button>
          <button className="cb-btn-inativar" onClick={() => handleDesativar(c.id)}>Desativar</button>
          <button className="cat-btn-excluir" onClick={() => handleExcluir(c)}>Excluir</button>
        </div>
      </div>
    )
  }

  if (loading) return <p style={{ color: 'var(--tx3)' }}>Carregando...</p>

  return (
    <>
      <h3 style={{ marginBottom: 16 }}>📋 Plano de Contas</h3>
      <p style={{ color: 'var(--tx3)', fontSize: 13, marginTop: -12, marginBottom: 16 }}>
        Hierarquia do DRE: Bloco (fixo) → Grupo (seu, editável aqui) → Categoria (nível de lançamento).
      </p>

      <div className="add-conta-form">
        <h4>＋ Nova Categoria</h4>
        <div className="conta-form-row">
          <input
            placeholder="Nome da categoria"
            value={novoNome}
            onChange={e => setNovoNome(e.target.value)}
            style={{ flex: 2, minWidth: 160 }}
          />
          <select
            value={novoBloco}
            onChange={e => {
              const bloco = e.target.value as Bloco
              setNovoBloco(bloco)
              const doBloco = gruposAtivos.filter(g => g.bloco === bloco)
              setNovoGrupoId(doBloco[0]?.id ?? '__novo__')
            }}
            style={{ flex: 1, minWidth: 190 }}
          >
            {BLOCOS_ORDEM.map(b => <option key={b} value={b}>{BLOCO_LABEL[b]}</option>)}
          </select>
          <select value={novoGrupoId} onChange={e => setNovoGrupoId(e.target.value)} style={{ flex: 1, minWidth: 170 }}>
            {gruposDoBlocoEscolhido.map(g => <option key={g.id} value={g.id}>{g.nome}</option>)}
            <option value="__novo__">＋ Novo grupo...</option>
          </select>
          {novoGrupoId === '__novo__' && (
            <input
              placeholder="Nome do novo grupo"
              value={novoGrupoNovoNome}
              onChange={e => setNovoGrupoNovoNome(e.target.value)}
              style={{ flex: 1, minWidth: 160 }}
              autoFocus
            />
          )}
          <button
            className="btn-add-conta"
            onClick={handleCriar}
            disabled={criando || !novoNome.trim() || !novoGrupoId || (novoGrupoId === '__novo__' && !novoGrupoNovoNome.trim())}
          >
            {criando ? 'Criando...' : '＋ Criar'}
          </button>
        </div>
        {msg && (
          <div style={{ marginTop: 8, fontSize: 13, fontWeight: 600, color: msgOk ? '#34c759' : '#ff6b6b' }}>
            {msg}
          </div>
        )}
      </div>

      <div className="cat-busca-wrap">
        <input
          type="search"
          placeholder="🔎 Buscar categoria por nome..."
          value={busca}
          onChange={e => setBusca(e.target.value)}
          className="cat-busca-input"
        />
      </div>

      {arvore.map(({ bloco, grupos: gruposDoBloco }) => {
        const blocoColapsado = blocosColapsados.has(bloco)
        const totalCategorias = gruposDoBloco.reduce((acc, g) => acc + g.categorias.length, 0)
        return (
          <div key={bloco} className="cat-bloco">
            <button type="button" className="cat-bloco-header" onClick={() => toggleBloco(bloco)}>
              <span className="cat-grupo-seta">{blocoColapsado ? '▸' : '▾'}</span>
              {BLOCO_LABEL[bloco]} ({totalCategorias})
            </button>
            {!blocoColapsado && (
              <div className="cat-bloco-corpo">
                {gruposDoBloco.map(({ grupo, categorias: categoriasDoGrupo }) => {
                  const grupoColapsado = gruposColapsados.has(grupo.id)
                  return (
                    <div key={grupo.id} className="cat-grupo">
                      <div className="cat-grupo-header cat-grupo-header-com-acoes">
                        <button type="button" className="cat-grupo-toggle" onClick={() => toggleGrupo(grupo.id)}>
                          <span className="cat-grupo-seta">{grupoColapsado ? '▸' : '▾'}</span>
                          {grupo.nome} ({categoriasDoGrupo.length})
                        </button>
                        <div className="cat-grupo-acoes">
                          <button className="cb-btn-editar" onClick={() => iniciarEdicaoGrupo(grupo)}>Editar grupo</button>
                          <button className="cb-btn-inativar" onClick={() => handleDesativarGrupo(grupo)}>Desativar grupo</button>
                        </div>
                      </div>
                      {!grupoColapsado && (
                        <div className="cat-lista-compacta">
                          {categoriasDoGrupo.length === 0
                            ? <p style={{ color: 'var(--tx3)', fontSize: 12, padding: '4px 4px' }}>Nenhuma categoria neste grupo ainda.</p>
                            : categoriasDoGrupo.map(renderCategoriaAtiva)}
                        </div>
                      )}
                    </div>
                  )
                })}
              </div>
            )}
          </div>
        )
      })}

      {inativasFiltradas.length > 0 && (
        <div className="contas-section">
          <h3 style={{ color: 'var(--tx3)' }}>⛔ Categorias Inativas ({inativasFiltradas.length})</h3>
          <div className="cat-lista-compacta">
            {inativasFiltradas.map(c => (
              <div key={c.id} className="cat-item-compacta cat-inativa">
                <DirecaoIcone ehEntrada={c.ehEntrada} ehSaida={c.ehSaida} />
                <span className="cat-nome-compacta" style={{ color: 'var(--tx3)' }}>{c.nome}</span>
                <span className="cat-tipo-compacta">{c.grupoNome}</span>
                <div className="cat-acoes-compactas">
                  <button className="cb-btn-editar" onClick={() => handleReativar(c)}>Reativar</button>
                  <button className="cat-btn-excluir" onClick={() => handleExcluir(c)}>Excluir</button>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      <Modal
        open={!!emUso}
        title="Categoria em uso"
        onClose={() => setEmUso(null)}
        footer={
          <>
            <button className="btn-cancel" onClick={() => setEmUso(null)}>Cancelar</button>
            <button className="btn-confirm" onClick={handleDesativarAoInves}>Só desativar</button>
            <button className="btn-confirm" onClick={handleMigrar} disabled={!destinoMigracao || migrando}>
              {migrando ? 'Migrando...' : 'Migrar e desativar'}
            </button>
          </>
        }
      >
        {emUso && (
          <>
            <p style={{ color: 'var(--tx3)', marginBottom: 12 }}>
              <strong>{emUso.quantidade}</strong> lançamento(s) usam a categoria "{emUso.categoria.nome}".
              Ela não pode ser excluída fisicamente sem corromper o histórico. Desative-a, ou migre esses
              lançamentos para outra categoria do mesmo grupo antes.
            </p>
            {opcoesMigracao.length > 0 ? (
              <div className="inp-group">
                <label>Migrar lançamentos para</label>
                <select value={destinoMigracao} onChange={e => setDestinoMigracao(e.target.value)}>
                  <option value="">Selecione...</option>
                  {opcoesMigracao.map(c => <option key={c.id} value={c.id}>{c.nome}</option>)}
                </select>
              </div>
            ) : (
              <p style={{ color: 'var(--tx3)', fontSize: 13 }}>
                Não há outra categoria ativa no grupo "{emUso.categoria.grupoNome}" para migrar.
              </p>
            )}
          </>
        )}
      </Modal>

      <Modal
        open={!!editGrupo}
        title="Editar grupo"
        onClose={() => setEditGrupo(null)}
        footer={
          <>
            <button className="btn-cancel" onClick={() => setEditGrupo(null)}>Cancelar</button>
            <button className="btn-confirm" onClick={handleSalvarEdicaoGrupo} disabled={salvandoGrupo || !editGrupoNome.trim()}>
              {salvandoGrupo ? 'Salvando...' : 'Salvar'}
            </button>
          </>
        }
      >
        {editGrupo && (
          <>
            <div className="inp-group">
              <label>Nome do grupo</label>
              <input value={editGrupoNome} onChange={e => setEditGrupoNome(e.target.value)} />
            </div>
            <div className="inp-group" style={{ marginTop: 12 }}>
              <label>Bloco</label>
              <select value={editGrupoBloco} onChange={e => setEditGrupoBloco(e.target.value as Bloco)}>
                {BLOCOS_ORDEM.map(b => <option key={b} value={b}>{BLOCO_LABEL[b]}</option>)}
              </select>
            </div>
            {editGrupoBloco !== editGrupo.bloco && editGrupo.quantidadeCategorias > 0 && (
              <p style={{ color: 'var(--warning)', fontSize: 12, marginTop: 10 }}>
                ⚠ {editGrupo.quantidadeCategorias} categoria(s) deste grupo vão mudar de classificação no DRE.
              </p>
            )}
          </>
        )}
      </Modal>
    </>
  )
}
