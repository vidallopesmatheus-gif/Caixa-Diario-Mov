import { useEffect, useRef, useState, forwardRef } from 'react'
import { criarCategoria } from '../../api/categorias'
import type { CategoriaItem, CategoriaAdmin, TipoCusto } from '../../types'
import './CategoriaCombobox.css'

interface CategoriaComboboxProps {
  categorias: CategoriaItem[]
  value: string
  onChange: (nome: string) => void
  onCategoriaCriada?: (categoria: CategoriaAdmin) => void
  tipoPadraoNovaCategoria: TipoCusto
  placeholder?: string
  onNavigate?: (direcao: 'up' | 'down') => void
}

const CategoriaCombobox = forwardRef<HTMLInputElement, CategoriaComboboxProps>(function CategoriaCombobox(
  { categorias, value, onChange, onCategoriaCriada, tipoPadraoNovaCategoria, placeholder, onNavigate },
  ref,
) {
  const [texto, setTexto] = useState(value)
  const [aberto, setAberto] = useState(false)
  const [indiceAtivo, setIndiceAtivo] = useState(0)
  const [criando, setCriando] = useState(false)
  const [tipoNova, setTipoNova] = useState<TipoCusto>(tipoPadraoNovaCategoria)
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

  function abrirCriacao() {
    setTipoNova(tipoPadraoNovaCategoria)
    setErro('')
    setCriando(true)
  }

  async function confirmarCriacao() {
    const nome = texto.trim()
    if (!nome) return
    setSalvando(true)
    setErro('')
    try {
      const nova = await criarCategoria(nome, tipoNova)
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
        <div className="cc-criar-form" onMouseDown={e => e.preventDefault()}>
          <div className="cc-criar-nome">Nova categoria: <strong>{texto.trim()}</strong></div>
          <select className="cc-criar-tipo" value={tipoNova} onChange={e => setTipoNova(e.target.value as TipoCusto)}>
            <option value="Receita">Receita</option>
            <option value="CustoVariavel">Custo Variável</option>
            <option value="CustoFixo">Custo Fixo</option>
            <option value="DespesaNaoOperacional">Despesa Não Operacional</option>
          </select>
          {erro && <div className="cc-criar-erro">{erro}</div>}
          <div className="cc-criar-acoes">
            <button type="button" className="cc-btn-criar" disabled={salvando} onClick={confirmarCriacao}>
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
