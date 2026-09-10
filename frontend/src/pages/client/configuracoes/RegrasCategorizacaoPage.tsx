import { useState, useEffect, useMemo } from 'react'
import { useAuth } from '../../../contexts/AuthContext'
import {
  listarRegras, atualizarRegra, desativarRegra, reativarRegra, excluirRegra, reordenarRegras,
  contarCorrespondencias, aplicarRegraRetroativamente,
} from '../../../api/regras'
import { listarContasBancarias } from '../../../api/contasBancarias'
import { listarCategorias } from '../../../api/categorias'
import CategoriaCombobox from '../../../components/shared/CategoriaCombobox'
import Modal from '../../../components/shared/Modal'
import type { RegraCategorizacao, ContaBancaria, Categorias } from '../../../types'
import '../ClientContasBancarias.css'
import '../ClientExtratoRevisao.css'

interface Props { clienteIdOverride?: string }

function descreverCriterio(regra: RegraCategorizacao): string {
  return regra.criterioTipo === 'Documento'
    ? `Mesmo CNPJ/CPF de "${regra.descricaoReferencia}"`
    : `Descrição igual a "${regra.descricaoReferencia}"`
}

/** Configurações → Regras de Categorização: listar, editar, (des)ativar, excluir, reordenar,
 *  aplicar retroativamente. Criação só acontece na tela de Categorizar Lançamentos — a regra
 *  sempre nasce de um lançamento real que o usuário está categorizando. */
export default function RegrasCategorizacaoPage({ clienteIdOverride }: Props) {
  const { user } = useAuth()
  const clienteId = clienteIdOverride ?? user?.usuarioId ?? null

  const [regras, setRegras] = useState<RegraCategorizacao[]>([])
  const [contasBancarias, setContasBancarias] = useState<ContaBancaria[]>([])
  const [categorias, setCategorias] = useState<Categorias>({ entradas: [], saidas: [] })
  const [loading, setLoading] = useState(true)
  const [msg, setMsg] = useState('')
  const [msgOk, setMsgOk] = useState(true)

  const [editRegra, setEditRegra] = useState<RegraCategorizacao | null>(null)
  const [editDescricaoReferencia, setEditDescricaoReferencia] = useState('')
  const [editAcaoTipo, setEditAcaoTipo] = useState<'Categoria' | 'Transferencia'>('Categoria')
  const [editCategoria, setEditCategoria] = useState('')
  const [editContaContrapartidaId, setEditContaContrapartidaId] = useState('')
  const [editAtiva, setEditAtiva] = useState(true)
  const [editContagem, setEditContagem] = useState<number | null>(null)
  const [salvandoEdit, setSalvandoEdit] = useState(false)

  const [aplicandoRetroativo, setAplicandoRetroativo] = useState<string | null>(null)

  useEffect(() => {
    if (!clienteId) return
    carregar()
    listarContasBancarias(clienteId).then(setContasBancarias).catch(() => {})
    listarCategorias().then(setCategorias).catch(() => {})
  }, [clienteId])

  function carregar() {
    if (!clienteId) return
    setLoading(true)
    listarRegras(clienteId)
      .then(setRegras)
      .catch(() => showMsg('Erro ao carregar regras.', false))
      .finally(() => setLoading(false))
  }

  function showMsg(texto: string, ok = true, duracaoMs = 4000) {
    setMsgOk(ok)
    setMsg(texto)
    setTimeout(() => setMsg(''), duracaoMs)
  }

  const ativas = useMemo(() => regras.filter(r => r.ativa).sort((a, b) => a.ordem - b.ordem), [regras])
  const inativas = useMemo(() => regras.filter(r => !r.ativa), [regras])

  function iniciarEdicao(r: RegraCategorizacao) {
    setEditRegra(r)
    setEditDescricaoReferencia(r.descricaoReferencia)
    setEditAcaoTipo(r.acaoTipo)
    setEditCategoria(r.categoria ?? '')
    setEditContaContrapartidaId(r.contaContrapartidaId ?? '')
    setEditAtiva(r.ativa)
    setEditContagem(null)
    setMsg('')
  }

  // Recalcula "quantos casariam" quando a descrição de referência (o critério) muda na edição.
  useEffect(() => {
    if (!editRegra || !editDescricaoReferencia.trim()) { setEditContagem(null); return }
    let cancelado = false
    contarCorrespondencias(editRegra.contaBancariaId, editRegra.tipo, editDescricaoReferencia)
      .then(qtd => { if (!cancelado) setEditContagem(qtd) })
      .catch(() => { if (!cancelado) setEditContagem(null) })
    return () => { cancelado = true }
  }, [editRegra, editDescricaoReferencia])

  async function handleSalvarEdicao() {
    if (!editRegra) return
    if (editAcaoTipo === 'Categoria' && !editCategoria) return
    if (editAcaoTipo === 'Transferencia' && !editContaContrapartidaId) return
    setSalvandoEdit(true)
    try {
      await atualizarRegra(editRegra.id, {
        descricaoReferencia: editDescricaoReferencia.trim(),
        acaoTipo: editAcaoTipo,
        categoria: editAcaoTipo === 'Categoria' ? editCategoria : undefined,
        contaContrapartidaId: editAcaoTipo === 'Transferencia' ? editContaContrapartidaId : undefined,
        ativa: editAtiva,
      })
      setEditRegra(null)
      showMsg('Regra atualizada!')
      carregar()
    } catch (e: unknown) {
      showMsg(e instanceof Error ? e.message : 'Erro ao atualizar regra.', false)
    } finally {
      setSalvandoEdit(false)
    }
  }

  async function handleDesativar(r: RegraCategorizacao) {
    if (!confirm(`Desativar a regra baseada em "${r.descricaoReferencia}"? Ela para de valer nas próximas importações — os lançamentos que ela já classificou não mudam.`)) return
    try {
      await desativarRegra(r.id)
      showMsg('Regra desativada.')
      carregar()
    } catch (e: unknown) {
      showMsg(e instanceof Error ? e.message : 'Erro ao desativar.', false)
    }
  }

  async function handleReativar(r: RegraCategorizacao) {
    try {
      await reativarRegra(r.id)
      showMsg('Regra reativada.')
      carregar()
    } catch (e: unknown) {
      showMsg(e instanceof Error ? e.message : 'Erro ao reativar.', false)
    }
  }

  async function handleExcluir(r: RegraCategorizacao) {
    if (!confirm(`Excluir a regra baseada em "${r.descricaoReferencia}"? Os lançamentos que ela já classificou continuam com a categoria que receberam — só a regra em si é removida.`)) return
    try {
      await excluirRegra(r.id)
      showMsg('Regra excluída.')
      carregar()
    } catch (e: unknown) {
      showMsg(e instanceof Error ? e.message : 'Erro ao excluir.', false)
    }
  }

  async function handleMover(id: string, direcao: -1 | 1) {
    const indice = ativas.findIndex(r => r.id === id)
    const alvo = indice + direcao
    if (indice < 0 || alvo < 0 || alvo >= ativas.length) return
    const nova = [...ativas]
    const tmp = nova[indice]
    nova[indice] = nova[alvo]
    nova[alvo] = tmp
    if (!clienteId) return
    try {
      await reordenarRegras(clienteId, [...nova.map(r => r.id), ...inativas.map(r => r.id)])
      carregar()
    } catch (e: unknown) {
      showMsg(e instanceof Error ? e.message : 'Erro ao reordenar.', false)
    }
  }

  async function handleAplicarRetroativo(r: RegraCategorizacao) {
    if (!confirm(`Aplicar esta regra a todos os lançamentos PENDENTES já importados nesta conta que casarem com o critério? Lançamentos já categorizados manualmente não são afetados.`)) return
    setAplicandoRetroativo(r.id)
    try {
      const total = await aplicarRegraRetroativamente(r.id)
      showMsg(total > 0 ? `${total} lançamento(s) categorizado(s) retroativamente.` : 'Nenhum lançamento pendente casava com o critério.')
      carregar()
    } catch (e: unknown) {
      showMsg(e instanceof Error ? e.message : 'Erro ao aplicar retroativamente.', false)
    } finally {
      setAplicandoRetroativo(null)
    }
  }

  function renderAcao(r: RegraCategorizacao) {
    return r.acaoTipo === 'Categoria'
      ? <span>{r.categoria}</span>
      : <span>🔁 Transferência → {r.contaContrapartidaNome ?? '—'}</span>
  }

  function renderRegra(r: RegraCategorizacao, i: number) {
    return (
      <div key={r.id} className="cat-item-compacta">
        <div className="cat-ordem-setas">
          <button disabled={i === 0} onClick={() => handleMover(r.id, -1)} title="Mover para cima">▲</button>
          <button disabled={i === ativas.length - 1} onClick={() => handleMover(r.id, 1)} title="Mover para baixo">▼</button>
        </div>
        <span className={r.tipo === 'Entrada' ? 'cat-direcao cat-direcao-entrada' : 'cat-direcao cat-direcao-saida'} title={r.tipo}>
          {r.tipo === 'Entrada' ? '↓' : '↑'}
        </span>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div className="cat-nome-compacta">{descreverCriterio(r)}</div>
          <div style={{ fontSize: 12, color: 'var(--tx3)' }}>
            {r.contaBancariaNome} · {renderAcao(r)} · {r.quantidadeAplicada} lançamento(s) já classificado(s)
          </div>
        </div>
        <div className="cat-acoes-compactas">
          <button className="cb-btn-editar" onClick={() => iniciarEdicao(r)}>Editar</button>
          <button
            className="cb-btn-editar"
            onClick={() => handleAplicarRetroativo(r)}
            disabled={aplicandoRetroativo === r.id}
          >
            {aplicandoRetroativo === r.id ? 'Aplicando...' : 'Aplicar retroativamente'}
          </button>
          <button className="cb-btn-inativar" onClick={() => handleDesativar(r)}>Desativar</button>
          <button className="cat-btn-excluir" onClick={() => handleExcluir(r)}>Excluir</button>
        </div>
      </div>
    )
  }

  if (loading) return <p style={{ color: 'var(--tx3)' }}>Carregando...</p>

  return (
    <>
      <h3 style={{ marginBottom: 8 }}>⚙️ Regras de Categorização Automática</h3>
      <p style={{ color: 'var(--tx3)', fontSize: 13, marginBottom: 16 }}>
        Nascem na tela Categorizar Lançamentos, a partir de um lançamento real. Aqui você edita,
        reordena (a primeira regra ativa que casar com um lançamento vence), desativa ou exclui.
      </p>

      {msg && <div style={{ marginBottom: 12, fontSize: 13, fontWeight: 600, color: msgOk ? '#34c759' : '#ff6b6b' }}>{msg}</div>}

      {ativas.length === 0 ? (
        <p style={{ color: 'var(--tx3)', fontSize: 13 }}>
          Nenhuma regra ativa ainda. Crie uma na tela Categorizar Lançamentos, ao classificar um lançamento ou grupo.
        </p>
      ) : (
        <div className="cat-lista-compacta">
          {ativas.map((r, i) => renderRegra(r, i))}
        </div>
      )}

      {inativas.length > 0 && (
        <div className="contas-section">
          <h3 style={{ color: 'var(--tx3)' }}>⛔ Regras Inativas ({inativas.length})</h3>
          <div className="cat-lista-compacta">
            {inativas.map(r => (
              <div key={r.id} className="cat-item-compacta cat-inativa">
                <span className={r.tipo === 'Entrada' ? 'cat-direcao cat-direcao-entrada' : 'cat-direcao cat-direcao-saida'}>
                  {r.tipo === 'Entrada' ? '↓' : '↑'}
                </span>
                <div style={{ flex: 1, minWidth: 0, color: 'var(--tx3)' }}>
                  {descreverCriterio(r)} · {r.contaBancariaNome} · {renderAcao(r)}
                </div>
                <div className="cat-acoes-compactas">
                  <button className="cb-btn-editar" onClick={() => handleReativar(r)}>Reativar</button>
                  <button className="cat-btn-excluir" onClick={() => handleExcluir(r)}>Excluir</button>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      <Modal
        open={!!editRegra}
        title="Editar regra"
        onClose={() => setEditRegra(null)}
        footer={
          <>
            <button className="btn-cancel" onClick={() => setEditRegra(null)}>Cancelar</button>
            <button
              className="btn-confirm"
              onClick={handleSalvarEdicao}
              disabled={salvandoEdit || !editDescricaoReferencia.trim() || (editAcaoTipo === 'Categoria' ? !editCategoria : !editContaContrapartidaId)}
            >
              {salvandoEdit ? 'Salvando...' : 'Salvar'}
            </button>
          </>
        }
      >
        {editRegra && (
          <>
            <div className="inp-group">
              <label>Descrição de referência (define o critério — documento se tiver CNPJ/CPF, senão descrição exata)</label>
              <input value={editDescricaoReferencia} onChange={e => setEditDescricaoReferencia(e.target.value)} style={{ width: '100%' }} />
            </div>

            <div className="inp-group" style={{ marginTop: 12 }}>
              <label>Ação</label>
              <div style={{ display: 'flex', gap: 8 }}>
                <button type="button" onClick={() => setEditAcaoTipo('Categoria')} className={editAcaoTipo === 'Categoria' ? 'btn-confirm' : 'btn-cancel'}>
                  Categorizar
                </button>
                <button type="button" onClick={() => setEditAcaoTipo('Transferencia')} className={editAcaoTipo === 'Transferencia' ? 'btn-confirm' : 'btn-cancel'}>
                  🔁 Transferência
                </button>
              </div>
            </div>

            {editAcaoTipo === 'Categoria' ? (
              <div className="inp-group" style={{ marginTop: 12 }}>
                <label>Categoria</label>
                <CategoriaCombobox
                  categorias={editRegra.tipo === 'Entrada' ? categorias.entradas : categorias.saidas}
                  value={editCategoria}
                  onChange={setEditCategoria}
                  blocoPadraoNovaCategoria={editRegra.tipo === 'Entrada' ? 'RECEITAS OPERACIONAIS' : 'DESPESAS OPERACIONAIS'}
                />
              </div>
            ) : (
              <div className="inp-group" style={{ marginTop: 12 }}>
                <label>Conta contrapartida</label>
                <select value={editContaContrapartidaId} onChange={e => setEditContaContrapartidaId(e.target.value)}>
                  <option value="">Selecione...</option>
                  {contasBancarias.filter(c => c.ativa && c.id !== editRegra.contaBancariaId).map(c => (
                    <option key={c.id} value={c.id}>{c.nome}</option>
                  ))}
                </select>
              </div>
            )}

            <label style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 12, fontSize: 13 }}>
              <input type="checkbox" checked={editAtiva} onChange={e => setEditAtiva(e.target.checked)} />
              Regra ativa
            </label>

            <p style={{ fontSize: 12, color: 'var(--tx3)', marginTop: 12 }}>
              {editContagem === null
                ? 'Calculando quantos lançamentos pendentes casam com esse critério...'
                : `${editContagem} lançamento(s) pendente(s) hoje casam com esse critério.`}
            </p>
          </>
        )}
      </Modal>
    </>
  )
}
