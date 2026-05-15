import { useState } from 'react'
import { useUsuarios } from '../../hooks/useUsuarios'
import Modal from '../../components/shared/Modal'
import type { Usuario } from '../../types'
import './Admin.css'

function ClientForm({ initial, onSave, onCancel }: {
  initial?: Partial<Usuario>
  onSave: (d: any) => void
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
      <div className="inp-group"><label>Nome completo *</label><input value={nomeCompleto} onChange={e => setNomeCompleto(e.target.value)} required /></div>
      <div className="inp-group"><label>Estabelecimento</label><input value={nomeEstabelecimento} onChange={e => setNomeEstabelecimento(e.target.value)} /></div>
      {!initial && <div className="inp-group"><label>Usuário *</label><input value={nomeUsuario} onChange={e => setNomeUsuario(e.target.value)} required /></div>}
      <div className="inp-group">
        <label>Senha {initial ? '(deixe em branco para manter)' : '*'}</label>
        <input type="password" value={senha} onChange={e => setSenha(e.target.value)} required={!initial} />
      </div>
      <div className="modal-footer">
        <button type="button" className="btn-cancel" onClick={onCancel}>Cancelar</button>
        <button type="submit" className="btn-confirm">Salvar</button>
      </div>
    </form>
  )
}

export default function AdminClientsPage() {
  const { usuarios, loading, criar, atualizar, desativar } = useUsuarios()
  const clientes = usuarios.filter(u => u.perfil === 'cliente')
  const [selected, setSelected] = useState<Usuario | null>(null)
  const [showAdd, setShowAdd] = useState(false)
  const [showDelete, setShowDelete] = useState(false)
  const [erro, setErro] = useState('')

  async function handleCriar(dto: any) {
    try { await criar(dto); setShowAdd(false) }
    catch (e: any) { setErro(e.message) }
  }

  async function handleAtualizar(dto: any) {
    if (!selected) return
    try { await atualizar(selected.id, dto); setSelected(null) }
    catch (e: any) { setErro(e.message) }
  }

  async function handleDesativar() {
    if (!selected) return
    try { await desativar(selected.id); setSelected(null); setShowDelete(false) }
    catch (e: any) { setErro(e.message) }
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
              {!u.ativo && <span style={{ fontSize: 11, color: '#ff6b6b' }}>Inativo</span>}
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
