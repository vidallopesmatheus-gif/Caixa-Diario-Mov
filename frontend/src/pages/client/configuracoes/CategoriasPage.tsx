import { useState, useEffect, useMemo } from 'react'
import {
  listarCategoriasParaGerenciar,
  criarCategoria,
  atualizarCategoria,
  desativarCategoria,
  reordenarCategorias,
  excluirCategoria,
  migrarCategoria,
} from '../../../api/categorias'
import Modal from '../../../components/shared/Modal'
import type { CategoriaAdmin, TipoCusto } from '../../../types'
import '../ClientContasBancarias.css'
import './Categorias.css'

const TIPOS: TipoCusto[] = ['Receita', 'CustoVariavel', 'CustoFixo', 'DespesaNaoOperacional']
const TIPO_LABEL: Record<TipoCusto, string> = {
  Receita: 'Receita',
  CustoVariavel: 'Custo Variável',
  CustoFixo: 'Despesa Fixa',
  DespesaNaoOperacional: 'Despesa Não Operacional',
}

// Mesma ordem usada no seed do Plano de Contas — grupos sem nome caem em "Outros".
const GRUPO_ORDEM = ['Custos Diretos', 'Pessoas', 'Despesas Administrativas', 'Marketing', 'Impostos', 'Financeiras', 'Investimentos', 'Outros']

export default function CategoriasPage() {
  const [categorias, setCategorias] = useState<CategoriaAdmin[]>([])
  const [loading, setLoading] = useState(true)
  const [msg, setMsg] = useState('')
  const [msgOk, setMsgOk] = useState(true)

  const [novoNome, setNovoNome] = useState('')
  const [novoTipo, setNovoTipo] = useState<TipoCusto>('CustoVariavel')
  const [criando, setCriando] = useState(false)

  const [editId, setEditId] = useState<string | null>(null)
  const [editNome, setEditNome] = useState('')
  const [editTipo, setEditTipo] = useState<TipoCusto>('CustoVariavel')
  const [salvandoEdit, setSalvandoEdit] = useState(false)

  const [emUso, setEmUso] = useState<{ categoria: CategoriaAdmin; quantidade: number } | null>(null)
  const [destinoMigracao, setDestinoMigracao] = useState('')
  const [migrando, setMigrando] = useState(false)

  const [busca, setBusca] = useState('')
  const [gruposColapsados, setGruposColapsados] = useState<Set<string>>(new Set())

  function toggleGrupo(nome: string) {
    setGruposColapsados(prev => {
      const next = new Set(prev)
      if (next.has(nome)) next.delete(nome)
      else next.add(nome)
      return next
    })
  }

  useEffect(() => { carregar() }, [])

  function carregar() {
    setLoading(true)
    listarCategoriasParaGerenciar()
      .then(setCategorias)
      .catch(() => showMsg('Erro ao carregar categorias.', false))
      .finally(() => setLoading(false))
  }

  function showMsg(texto: string, ok = true) {
    setMsgOk(ok)
    setMsg(texto)
    setTimeout(() => setMsg(''), 3000)
  }

  const ativas = useMemo(() => categorias.filter(c => c.ativa).sort((a, b) => a.ordem - b.ordem), [categorias])
  const inativas = useMemo(() => categorias.filter(c => !c.ativa), [categorias])
  // Índice na lista FLAT de ativas (não a filtrada/agrupada) — as setas ▲▼ reordenam globalmente,
  // então o estado desabilitado precisa continuar refletindo a posição real, não a exibida.
  const indicePorId = useMemo(() => new Map(ativas.map((c, i) => [c.id, i])), [ativas])

  const buscaNormalizada = busca.trim().toLowerCase()
  const ativasFiltradas = useMemo(
    () => buscaNormalizada ? ativas.filter(c => c.nome.toLowerCase().includes(buscaNormalizada)) : ativas,
    [ativas, buscaNormalizada]
  )
  const inativasFiltradas = useMemo(
    () => buscaNormalizada ? inativas.filter(c => c.nome.toLowerCase().includes(buscaNormalizada)) : inativas,
    [inativas, buscaNormalizada]
  )

  // Receita não tem grupo — fica em seção própria. As demais são agrupadas pelo campo `grupo`
  // (categorias sem grupo definido, ex.: criadas manualmente, caem em "Outros").
  const receitasAtivas = useMemo(() => ativasFiltradas.filter(c => c.tipo === 'Receita'), [ativasFiltradas])
  const gruposDespesas = useMemo(() => {
    const porGrupo = new Map<string, CategoriaAdmin[]>()
    for (const c of ativasFiltradas) {
      if (c.tipo === 'Receita') continue
      const chave = c.grupo ?? 'Outros'
      const lista = porGrupo.get(chave)
      if (lista) lista.push(c)
      else porGrupo.set(chave, [c])
    }
    return GRUPO_ORDEM.filter(g => porGrupo.has(g)).map(nome => ({ nome, itens: porGrupo.get(nome)! }))
  }, [ativasFiltradas])

  async function handleCriar() {
    if (!novoNome.trim()) return
    setCriando(true)
    try {
      await criarCategoria(novoNome.trim(), novoTipo)
      setNovoNome('')
      setNovoTipo('CustoVariavel')
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
    setEditTipo(c.tipo)
  }

  async function handleSalvarEdit() {
    if (!editId) return
    setSalvandoEdit(true)
    try {
      await atualizarCategoria(editId, editNome.trim(), editTipo, true)
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
      await atualizarCategoria(c.id, c.nome, c.tipo, true)
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

  const opcoesMigracao = emUso
    ? ativas.filter(c => c.id !== emUso.categoria.id && c.tipo === emUso.categoria.tipo)
    : []

  function renderCategoriaAtiva(c: CategoriaAdmin) {
    const i = indicePorId.get(c.id) ?? 0
    if (editId === c.id) {
      return (
        <div key={c.id} className="cat-item-compacta cat-edit-form">
          <input value={editNome} onChange={e => setEditNome(e.target.value)} style={{ flex: 2 }} />
          <select value={editTipo} onChange={e => setEditTipo(e.target.value as TipoCusto)} style={{ flex: 1, minWidth: 180 }}>
            {TIPOS.map(t => <option key={t} value={t}>{TIPO_LABEL[t]}</option>)}
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
        <span className="cat-nome-compacta">{c.nome}</span>
        <span className="cat-tipo-compacta">{TIPO_LABEL[c.tipo]}</span>
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

      <div className="add-conta-form">
        <h4>＋ Nova Categoria</h4>
        <div className="conta-form-row">
          <input
            placeholder="Nome da categoria"
            value={novoNome}
            onChange={e => setNovoNome(e.target.value)}
            style={{ flex: 2 }}
          />
          <select value={novoTipo} onChange={e => setNovoTipo(e.target.value as TipoCusto)} style={{ flex: 1, minWidth: 180 }}>
            {TIPOS.map(t => <option key={t} value={t}>{TIPO_LABEL[t]}</option>)}
          </select>
          <button className="btn-add-conta" onClick={handleCriar} disabled={criando || !novoNome.trim()}>
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

      <div className="contas-section">
        <h3>Receita ({receitasAtivas.length})</h3>
        {receitasAtivas.length === 0 && <p style={{ color: 'var(--tx3)', fontSize: 13 }}>Nenhuma categoria de receita{buscaNormalizada ? ' encontrada.' : '.'}</p>}
        <div className="cat-lista-compacta">
          {receitasAtivas.map(renderCategoriaAtiva)}
        </div>
      </div>

      <div className="contas-section">
        <h3>Custos e Despesas ({ativasFiltradas.length - receitasAtivas.length})</h3>
        {gruposDespesas.length === 0 && (
          <p style={{ color: 'var(--tx3)', fontSize: 13 }}>Nenhuma categoria{buscaNormalizada ? ' encontrada.' : ' ativa.'}</p>
        )}
        {gruposDespesas.map(grupo => {
          const colapsado = gruposColapsados.has(grupo.nome)
          return (
            <div key={grupo.nome} className="cat-grupo">
              <button type="button" className="cat-grupo-header" onClick={() => toggleGrupo(grupo.nome)}>
                <span className="cat-grupo-seta">{colapsado ? '▸' : '▾'}</span>
                {grupo.nome} ({grupo.itens.length})
              </button>
              {!colapsado && (
                <div className="cat-lista-compacta">
                  {grupo.itens.map(renderCategoriaAtiva)}
                </div>
              )}
            </div>
          )
        })}
      </div>

      {inativasFiltradas.length > 0 && (
        <div className="contas-section">
          <h3 style={{ color: 'var(--tx3)' }}>⛔ Categorias Inativas ({inativasFiltradas.length})</h3>
          <div className="cat-lista-compacta">
            {inativasFiltradas.map(c => (
              <div key={c.id} className="cat-item-compacta cat-inativa">
                <span className="cat-nome-compacta" style={{ color: 'var(--tx3)' }}>{c.nome}</span>
                <span className="cat-tipo-compacta">{TIPO_LABEL[c.tipo]}</span>
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
              lançamentos para outra categoria do mesmo tipo antes.
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
                Não há outra categoria ativa do mesmo tipo ({TIPO_LABEL[emUso.categoria.tipo]}) para migrar.
              </p>
            )}
          </>
        )}
      </Modal>
    </>
  )
}
