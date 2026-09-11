import { useState, useEffect, useCallback, useMemo } from 'react'
import { useParams } from 'react-router-dom'
import { obterPortalConciliacao, classificarPendentePortal } from '../../api/portalConciliacao'
import { PortalApiError } from '../../api/portalClient'
import { agruparPorDescricaoSimilar } from '../../utils/descricaoSimilar'
import { fmtBRL } from '../../utils/format'
import type { PortalConciliacaoData, ContaPendentesPortal, PendenteCategorizacao, CategoriaItem } from '../../types'
import './Portal.css'

function fmtData(iso: string): string {
  return iso.slice(0, 10).split('-').reverse().join('/')
}

function fmtDataHora(iso: string): string {
  return new Date(iso).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })
}

const CODIGOS_LINK_INVALIDO = new Set([
  'LINK_CONCILIACAO_NAO_ENCONTRADO',
  'LINK_CONCILIACAO_EXPIRADO',
  'LINK_CONCILIACAO_REVOGADO',
])

export default function PortalConciliacaoPage() {
  const { token } = useParams<{ token: string }>()
  const [dados, setDados] = useState<PortalConciliacaoData | null>(null)
  const [carregando, setCarregando] = useState(true)
  const [linkInvalido, setLinkInvalido] = useState(false)
  const [erroCarregar, setErroCarregar] = useState('')
  const [salvandoIds, setSalvandoIds] = useState<Set<string>>(new Set())
  const [erroSalvar, setErroSalvar] = useState('')

  const carregar = useCallback(async () => {
    if (!token) return
    setCarregando(true)
    setErroCarregar('')
    setLinkInvalido(false)
    try {
      const res = await obterPortalConciliacao(token)
      setDados(res)
    } catch (e: unknown) {
      if (e instanceof PortalApiError && CODIGOS_LINK_INVALIDO.has(e.codigo)) {
        setLinkInvalido(true)
      } else {
        setErroCarregar('Não foi possível carregar suas pendências agora. Tente novamente em instantes.')
      }
    } finally {
      setCarregando(false)
    }
  }, [token])

  useEffect(() => { carregar() }, [carregar])

  const totalPendentes = useMemo(
    () => dados?.contas.reduce((s, c) => s + c.itens.length, 0) ?? 0,
    [dados],
  )

  async function classificar(conta: ContaPendentesPortal, itens: PendenteCategorizacao[], categoria: string) {
    if (!token || itens.length === 0) return
    setSalvandoIds(prev => new Set([...prev, ...itens.map(i => i.id)]))
    setErroSalvar('')
    try {
      await classificarPendentePortal(token, itens.map(i => ({ id: i.id, data: i.data, contaBancariaId: conta.contaBancariaId, categoria })))
      const idsAplicados = new Set(itens.map(i => i.id))
      setDados(prev => {
        if (!prev) return prev
        return {
          ...prev,
          contas: prev.contas
            .map(c => c.contaBancariaId === conta.contaBancariaId
              ? { ...c, itens: c.itens.filter(i => !idsAplicados.has(i.id)) }
              : c)
            .filter(c => c.itens.length > 0),
        }
      })
    } catch {
      setErroSalvar('Não deu pra salvar essa categoria agora. Tente de novo.')
    } finally {
      setSalvandoIds(prev => {
        const next = new Set(prev)
        itens.forEach(i => next.delete(i.id))
        return next
      })
    }
  }

  if (carregando) {
    return (
      <div className="portal-wrap">
        <div className="portal-box portal-box-centro"><p className="portal-tx3">Carregando...</p></div>
      </div>
    )
  }

  if (linkInvalido) {
    return (
      <div className="portal-wrap">
        <div className="portal-box portal-box-centro">
          <div className="portal-icone">🔒</div>
          <h1 className="portal-titulo">Este link expirou</h1>
          <p className="portal-tx3">Peça um novo ao seu consultor.</p>
        </div>
      </div>
    )
  }

  if (erroCarregar || !dados) {
    return (
      <div className="portal-wrap">
        <div className="portal-box portal-box-centro">
          <p className="portal-tx3">{erroCarregar || 'Não foi possível carregar esta página.'}</p>
          <button className="portal-btn-primario" onClick={carregar}>Tentar de novo</button>
        </div>
      </div>
    )
  }

  return (
    <div className="portal-wrap">
      <div className="portal-box">
        <header className="portal-header">
          <h1 className="portal-titulo">Classificar lançamentos</h1>
          <p className="portal-tx3">Link válido até {fmtDataHora(dados.expiraEm)}</p>
        </header>

        {totalPendentes === 0 ? (
          <div className="portal-vazio">
            <div className="portal-icone">🎉</div>
            <p>Tudo classificado — obrigado!</p>
          </div>
        ) : (
          <>
            <p className="portal-contador">
              <strong>{totalPendentes}</strong> lançamento(s) aguardando você
            </p>
            {erroSalvar && <div className="portal-msg-erro">{erroSalvar}</div>}
            {dados.contas.map(conta => (
              <ContaSecao
                key={conta.contaBancariaId}
                conta={conta}
                categoriasEntrada={dados.categoriasEntrada}
                categoriasSaida={dados.categoriasSaida}
                salvandoIds={salvandoIds}
                onClassificar={(itens, categoria) => classificar(conta, itens, categoria)}
              />
            ))}
          </>
        )}
      </div>
    </div>
  )
}

function ContaSecao({
  conta, categoriasEntrada, categoriasSaida, salvandoIds, onClassificar,
}: {
  conta: ContaPendentesPortal
  categoriasEntrada: CategoriaItem[]
  categoriasSaida: CategoriaItem[]
  salvandoIds: Set<string>
  onClassificar: (itens: PendenteCategorizacao[], categoria: string) => void
}) {
  const grupos = useMemo(() => agruparPorDescricaoSimilar(conta.itens), [conta.itens])

  return (
    <section className="portal-conta">
      <h2 className="portal-conta-titulo">{conta.contaNome}</h2>
      <div className="portal-lista">
        {grupos.map(g => {
          const categorias = g.itens[0].tipo === 'Entrada' ? categoriasEntrada : categoriasSaida
          if (g.itens.length === 1) {
            const item = g.itens[0]
            return (
              <PortalLinhaPendente
                key={item.id}
                item={item}
                categorias={categorias}
                salvando={salvandoIds.has(item.id)}
                onCategorizar={cat => onClassificar([item], cat)}
              />
            )
          }
          return (
            <div className="portal-grupo" key={g.chave}>
              <div className="portal-grupo-header">
                <span className="portal-grupo-titulo">
                  {g.itens.length} parecidos: "{g.itens[0].descricao}"
                </span>
                <PortalCategoriaSelect
                  categorias={categorias}
                  placeholder="Categorizar todos..."
                  onSelect={cat => onClassificar(g.itens, cat)}
                />
              </div>
              {g.itens.map(item => (
                <PortalLinhaPendente
                  key={item.id}
                  item={item}
                  categorias={categorias}
                  salvando={salvandoIds.has(item.id)}
                  onCategorizar={cat => onClassificar([item], cat)}
                />
              ))}
            </div>
          )
        })}
      </div>
    </section>
  )
}

function PortalLinhaPendente({
  item, categorias, salvando, onCategorizar,
}: {
  item: PendenteCategorizacao
  categorias: CategoriaItem[]
  salvando: boolean
  onCategorizar: (categoria: string) => void
}) {
  return (
    <div className="portal-item">
      <div className="portal-item-topo">
        <span className="portal-item-data">{fmtData(item.data)}</span>
        <span className={`portal-item-valor ${item.tipo === 'Entrada' ? 'portal-valor-verde' : 'portal-valor-vermelho'}`}>
          {item.tipo === 'Entrada' ? '+' : '-'}{fmtBRL(item.valor)}
        </span>
      </div>
      <div className="portal-item-desc">{item.descricao}</div>
      <PortalCategoriaSelect
        categorias={categorias}
        placeholder={salvando ? 'Salvando...' : 'O que foi isso?'}
        disabled={salvando}
        onSelect={onCategorizar}
      />
    </div>
  )
}

function PortalCategoriaSelect({
  categorias, placeholder, disabled, onSelect,
}: {
  categorias: CategoriaItem[]
  placeholder?: string
  disabled?: boolean
  onSelect: (categoria: string) => void
}) {
  return (
    <select
      className="portal-cat-select"
      value=""
      disabled={disabled}
      onChange={e => { if (e.target.value) onSelect(e.target.value) }}
    >
      <option value="">{placeholder ?? 'Categoria...'}</option>
      {categorias.map(c => <option key={c.nome} value={c.nome}>{c.nome}</option>)}
    </select>
  )
}
