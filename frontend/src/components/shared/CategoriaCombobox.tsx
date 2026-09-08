import { useEffect, useRef, useState, forwardRef } from 'react'
import { criarCategoria, listarGrupos } from '../../api/categorias'
import type { CategoriaItem, CategoriaAdmin, Bloco, Grupo } from '../../types'
import { BLOCOS_ORDEM } from '../../types'
import './CategoriaCombobox.css'

const BLOCO_LABEL_CURTO: Record<Bloco, string> = {
  'RECEITAS OPERACIONAIS': 'Receitas',
  'DEDUÇÕES DA RECEITA': 'Deduções',
  'CUSTOS OPERACIONAIS': 'Custos',
  'DESPESAS OPERACIONAIS': 'Despesas',
  'ATIVIDADES DE INVESTIMENTO': 'Investimento',
  'ATIVIDADES DE FINANCIAMENTO': 'Financiamento',
}

interface CategoriaComboboxProps {
  categorias: CategoriaItem[]
  value: string
  onChange: (nome: string) => void
  onCategoriaCriada?: (categoria: CategoriaAdmin) => void
  blocoPadraoNovaCategoria: Bloco
  placeholder?: string
  onNavigate?: (direcao: 'up' | 'down') => void
}

const CategoriaCombobox = forwardRef<HTMLInputElement, CategoriaComboboxProps>(function CategoriaCombobox(
  { categorias, value, onChange, onCategoriaCriada, blocoPadraoNovaCategoria, placeholder, onNavigate },
  ref,
) {
  const [texto, setTexto] = useState(value)
  const [aberto, setAberto] = useState(false)
  const [indiceAtivo, setIndiceAtivo] = useState(0)
  const [criando, setCriando] = useState(false)
  const [grupos, setGrupos] = useState<Grupo[]>([])
  const [grupoNovaId, setGrupoNovaId] = useState('')
  const [erro, setErro] = useState('')
  const [salvando, setSalvando] = useState(false)
  const wrapperRef = useRef<HTMLDivElement>(null)

  useEffect(() => { setTexto(value) }, [value])

  const filtradas = texto.trim() === ''
    ? categorias
    : categorias.filter(c => c.nome.toLowerCase().includes(texto.trim().toLowerCase()))

  const temMatchExato = categorias.some(c => c.nome.toLowerCase() === texto.trim().toLowerCase())
  const mostrarCriar = texto.trim() !== '' && !temMatchExato
  const totalOpcoes = filtradas.length + (mostrarCriar ? 1 : 0)

  function selecionar(nome: string) {
    onChange(nome)
    setTexto(nome)
    setAberto(false)
  }

  async function abrirCriacao() {
    setErro('')
    setCriando(true)
    if (grupos.length === 0) {
      try {
        const todos = await listarGrupos()
        const ativos = todos.filter(g => g.ativo)
        setGrupos(ativos)
        setGrupoNovaId(ativos.find(g => g.bloco === blocoPadraoNovaCategoria)?.id ?? ativos[0]?.id ?? '')
      } catch {
        setErro('Erro ao carregar grupos do Plano de Contas.')
      }
    }
  }

  async function confirmarCriacao() {
    const nome = texto.trim()
    if (!nome || !grupoNovaId) return
    setSalvando(true)
    setErro('')
    try {
      const nova = await criarCategoria(nome, grupoNovaId)
      onCategoriaCriada?.(nova)
      selecionar(nova.nome)
      setCriando(false)
    } catch (e: unknown) {
      setErro(e instanceof Error ? e.message : 'Erro ao criar categoria.')
    } finally {
      setSalvando(false)
    }
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (criando) return
    if (e.key === 'ArrowDown') {
      if (!aberto) {
        if (onNavigate) { e.preventDefault(); onNavigate('down') }
        return
      }
      e.preventDefault()
      setIndiceAtivo(i => Math.min(i + 1, totalOpcoes - 1))
    } else if (e.key === 'ArrowUp') {
      if (!aberto) {
        if (onNavigate) { e.preventDefault(); onNavigate('up') }
        return
      }
      e.preventDefault()
      setIndiceAtivo(i => Math.max(i - 1, 0))
    } else if (e.key === 'Enter') {
      e.preventDefault()
      if (!aberto) return
      if (indiceAtivo < filtradas.length) selecionar(filtradas[indiceAtivo].nome)
      else if (mostrarCriar) abrirCriacao()
    } else if (e.key === 'Escape') {
      setTexto(value)
      setAberto(false)
    }
  }

  return (
    <div className="cc-wrapper" ref={wrapperRef}>
      <input
        ref={ref}
        className="cc-input"
        value={texto}
        placeholder={placeholder ?? 'Buscar categoria...'}
        onChange={e => { setTexto(e.target.value); setAberto(true); setIndiceAtivo(0) }}
        onFocus={e => { setAberto(true); e.target.select() }}
        onBlur={() => { setTimeout(() => setAberto(false), 150) }}
        onKeyDown={handleKeyDown}
      />
      {aberto && !criando && (
        <ul className="cc-lista" role="listbox">
          {filtradas.length === 0 && !mostrarCriar && (
            <li className="cc-vazio">Nenhuma categoria encontrada</li>
          )}
          {filtradas.map((c, i) => (
            <li
              key={c.nome}
              className={`cc-opcao ${i === indiceAtivo ? 'cc-opcao-ativa' : ''}`}
              onMouseDown={e => e.preventDefault()}
              onClick={() => selecionar(c.nome)}
              role="option"
              aria-selected={i === indiceAtivo}
            >
              {c.nome}
            </li>
          ))}
          {mostrarCriar && (
            <li
              className={`cc-opcao cc-opcao-criar ${indiceAtivo === filtradas.length ? 'cc-opcao-ativa' : ''}`}
              onMouseDown={e => e.preventDefault()}
              onClick={abrirCriacao}
              role="option"
              aria-selected={indiceAtivo === filtradas.length}
            >
              + Criar categoria "{texto.trim()}"
            </li>
          )}
        </ul>
      )}
      {criando && (
        <div className="cc-criar-form">
          <div className="cc-criar-nome">Nova categoria: <strong>{texto.trim()}</strong></div>
          <select className="cc-criar-tipo" value={grupoNovaId} onChange={e => setGrupoNovaId(e.target.value)}>
            {grupos.length === 0 && <option value="">Carregando grupos...</option>}
            {BLOCOS_ORDEM.map(bloco => {
              const doBloco = grupos.filter(g => g.bloco === bloco)
              if (doBloco.length === 0) return null
              return (
                <optgroup key={bloco} label={BLOCO_LABEL_CURTO[bloco]}>
                  {doBloco.map(g => <option key={g.id} value={g.id}>{g.nome}</option>)}
                </optgroup>
              )
            })}
          </select>
          {erro && <div className="cc-criar-erro">{erro}</div>}
          <div className="cc-criar-acoes">
            <button type="button" className="cc-btn-criar" disabled={salvando || !grupoNovaId} onClick={confirmarCriacao}>
              {salvando ? 'Criando...' : 'Criar e usar'}
            </button>
            <button type="button" className="cc-btn-cancelar" onClick={() => { setCriando(false); setAberto(false) }}>
              Cancelar
            </button>
          </div>
        </div>
      )}
    </div>
  )
})

export default CategoriaCombobox
