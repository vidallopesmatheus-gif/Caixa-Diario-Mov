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

      <div className="contas-section">
        <h3>Categorias Ativas ({ativas.length})</h3>
        {ativas.length === 0 && <p style={{ color: 'var(--tx3)', fontSize: 13 }}>Nenhuma categoria ativa.</p>}
        {ativas.map((c, i) => (
          <div key={c.id} className="cat-item">
            {editId === c.id ? (
              <div className="cat-edit-form">
                <input value={editNome} onChange={e => setEditNome(e.target.value)} style={{ flex: 2 }} />
                <select value={editTipo} onChange={e => setEditTipo(e.target.value as TipoCusto)} style={{ flex: 1, minWidth: 180 }}>
                  {TIPOS.map(t => <option key={t} value={t}>{TIPO_LABEL[t]}</option>)}
                </select>
                <button className="btn-add-conta" onClick={handleSalvarEdit} disabled={salvandoEdit}>
                  {salvandoEdit ? 'Salvando...' : '✔ Salvar'}
                </button>
                <button onClick={() => setEditId(null)} className="cat-btn-cancelar">Cancelar</button>
              </div>
            ) : (
              <>
                <div className="cat-ordem-setas">
                  <button disabled={i === 0} onClick={() => handleMover(c.id, -1)} title="Mover para cima">▲</button>
                  <button disabled={i === ativas.length - 1} onClick={() => handleMover(c.id, 1)} title="Mover para baixo">▼</button>
                </div>
                <div className="cat-info">
                  <div className="cat-nome">{c.nome}</div>
                  <div className="cat-meta">{TIPO_LABEL[c.tipo]}{c.grupo ? ` · ${c.grupo}` : ''}</div>
                </div>
                <div className="cat-acoes">
                  <button className="cb-btn-editar" onClick={() => iniciarEdicao(c)}>Editar</button>
                  <button className="cb-btn-inativar" onClick={() => handleDesativar(c.id)}>Desativar</button>
                  <button className="cat-btn-excluir" onClick={() => handleExcluir(c)}>Excluir</button>
                </div>
              </>
            )}
          </div>
        ))}
      </div>

      {inativas.length > 0 && (
        <div className="contas-section">
          <h3 style={{ color: 'var(--tx3)' }}>⛔ Categorias Inativas ({inativas.length})</h3>
          {inativas.map(c => (
            <div key={c.id} className="cat-item cat-inativa">
              <div className="cat-info">
                <div className="cat-nome" style={{ color: 'var(--tx3)' }}>{c.nome}</div>
                <div className="cat-meta">{TIPO_LABEL[c.tipo]} · Inativa</div>
              </div>
              <div className="cat-acoes">
                <button className="cb-btn-editar" onClick={() => handleReativar(c)}>Reativar</button>
                <button className="cat-btn-excluir" onClick={() => handleExcluir(c)}>Excluir</button>
              </div>
            </div>
          ))}
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
