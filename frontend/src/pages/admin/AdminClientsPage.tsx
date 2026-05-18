import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useUsuarios } from '../../hooks/useUsuarios'
import Modal from '../../components/shared/Modal'
import type { Usuario } from '../../types'
import './Admin.css'

interface ClientFormData {
  nomeCompleto: string
  nomeEstabelecimento: string
  nomeUsuario?: string
  senha?: string
  perfil?: string
}

function ClientForm({ initial, onSave, onCancel }: {
  initial?: Partial<Usuario>
  onSave: (d: ClientFormData) => void
  onCancel: () => void
}) {
  const [nomeCompleto, setNomeCompleto] = useState(initial?.nomeCompleto ?? '')
  const [nomeEstabelecimento, setNomeEstabelecimento] = useState(initial?.nomeEstabelecimento ?? '')
  const [nomeUsuario, setNomeUsuario] = useState(initial?.nomeUsuario ?? '')
  const [senha, setSenha] = useState('')
  const [erro, setErro] = useState('')

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!nomeCompleto || (!initial && !nomeUsuario)) { setErro('Preencha todos os campos obrigatórios.'); return }
    onSave({ nomeCompleto, nomeEstabelecimento, nomeUsuario, senha: senha || undefined, perfil: 'cliente' })
  }

  return (
    <form onSubmit={handleSubmit}>
      {erro && <div style={{ color: '#ff6b6b', marginBottom: 12, fontSize: 13 }}>{erro}</div>}
      <div className="inp-group"><label htmlFor="cf-nome">Nome completo *</label><input id="cf-nome" value={nomeCompleto} onChange={e => setNomeCompleto(e.target.value)} required /></div>
      <div className="inp-group"><label htmlFor="cf-estab">Estabelecimento</label><input id="cf-estab" value={nomeEstabelecimento} onChange={e => setNomeEstabelecimento(e.target.value)} /></div>
      {!initial && <div className="inp-group"><label htmlFor="cf-usuario">Usuário *</label><input id="cf-usuario" value={nomeUsuario} onChange={e => setNomeUsuario(e.target.value)} required /></div>}
      <div className="inp-group">
        <label htmlFor="cf-senha">Senha {initial ? '(deixe em branco para manter)' : '*'}</label>
        <input id="cf-senha" type="password" value={senha} onChange={e => setSenha(e.target.value)} required={!initial} />
      </div>
      <div className="modal-footer">
        <button type="button" className="btn-cancel" onClick={onCancel}>Cancelar</button>
        <button type="submit" className="btn-confirm">Salvar</button>
      </div>
    </form>
  )
}

export default function AdminClientsPage() {
  const navigate = useNavigate()
  const { usuarios, loading, criar, atualizar, desativar } = useUsuarios()
  const clientes = usuarios.filter(u => u.perfil === 'cliente')
  const [selected, setSelected] = useState<Usuario | null>(null)
  const [showAdd, setShowAdd] = useState(false)
  const [showDelete, setShowDelete] = useState(false)
  const [erro, setErro] = useState('')

  async function handleCriar(dto: ClientFormData) {
    try { await criar({ nomeCompleto: dto.nomeCompleto, nomeEstabelecimento: dto.nomeEstabelecimento, nomeUsuario: dto.nomeUsuario!, senha: dto.senha!, perfil: dto.perfil! }); setShowAdd(false) }
    catch (e: unknown) { setErro(e instanceof Error ? e.message : String(e)) }
  }

  async function handleAtualizar(dto: ClientFormData) {
    if (!selected) return
    try { await atualizar(selected.id, { nomeCompleto: dto.nomeCompleto, nomeEstabelecimento: dto.nomeEstabelecimento, senha: dto.senha }); setSelected(null) }
    catch (e: unknown) { setErro(e instanceof Error ? e.message : String(e)) }
  }

  async function handleDesativar() {
    if (!selected) return
    try { await desativar(selected.id); setSelected(null); setShowDelete(false) }
    catch (e: unknown) { setErro(e instanceof Error ? e.message : String(e)) }
  }

  if (loading) return <p style={{ color: 'var(--tx3)' }}>Carregando...</p>

  return (
    <>
      {erro && <div style={{ color: '#ff6b6b', marginBottom: 12 }}>{erro}</div>}
      <div className="admin-grid">
        <div className="panel">
          <div className="panel-title">
            <span>👥 Clientes</span>
            <span style={{ fontSize: 12, color: '#666' }}>{clientes.length} clientes</span>
          </div>
          {clientes.map(u => (
            <div key={u.id}
              className={`client-list-item${selected?.id === u.id ? ' selected' : ''}`}
              onClick={() => setSelected(u)}>
              <div>
                <div className="client-name">{u.nomeCompleto}</div>
                <div className="client-store">{u.nomeEstabelecimento}</div>
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                {!u.ativo && <span style={{ fontSize: 11, color: '#ff6b6b' }}>Inativo</span>}
                <button
                  className="btn-sm"
                  onClick={e => { e.stopPropagation(); navigate(`/admin/caixa/${u.id}`) }}
                  title="Ver caixa deste cliente"
                >
                  📋 Ver Caixa
                </button>
              </div>
            </div>
          ))}
          <button className="btn-add-client" onClick={() => setShowAdd(true)}>＋ Novo cliente</button>
        </div>

        <div className="panel">
          {selected ? (
            <>
              <div className="panel-title">
                <span>{selected.nomeCompleto}</span>
                {selected.ativo && (
                  <button className="btn-sm"
                    style={{ background: 'var(--rm-bg)', border: '1px solid var(--rm-bd)', color: '#ff6b6b' }}
                    onClick={() => setShowDelete(true)}>Excluir</button>
                )}
              </div>
              <ClientForm initial={selected} onSave={handleAtualizar} onCancel={() => setSelected(null)} />
            </>
          ) : (
            <>
              <div className="panel-title">Selecione um cliente</div>
              <p style={{ color: '#666', fontSize: 14 }}>Clique em um cliente para ver ou editar.</p>
            </>
          )}
        </div>
      </div>

      <Modal open={showAdd} title="Novo cliente" onClose={() => setShowAdd(false)}>
        <ClientForm onSave={handleCriar} onCancel={() => setShowAdd(false)} />
      </Modal>

      <Modal open={showDelete} title="Confirmar exclusão" onClose={() => setShowDelete(false)}
        footer={
          <>
            <button className="btn-cancel" onClick={() => setShowDelete(false)}>Cancelar</button>
            <button className="btn-danger" onClick={handleDesativar}>Excluir</button>
          </>
        }>
        <p>Deseja desativar o cliente <strong>{selected?.nomeCompleto}</strong>?</p>
      </Modal>
    </>
  )
}
