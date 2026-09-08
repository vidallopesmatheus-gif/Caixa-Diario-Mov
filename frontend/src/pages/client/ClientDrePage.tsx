import { useState, useEffect, useCallback, useMemo } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { obterDre } from '../../api/metricas'
import { listarContasBancarias } from '../../api/contasBancarias'
import { useRegistros } from '../../hooks/useRegistros'
import { fmtBRL, fmtPct, fmtDate } from '../../utils/format'
import Modal from '../../components/shared/Modal'
import type { Dre, DreBloco } from '../../api/metricas'
import type { ContaBancaria, Bloco } from '../../types'
import './ClientDre.css'

type TipoPeriodo = 'mes' | 'trimestre' | 'ano'

const MESES = ['Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun', 'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez']

// Cor de identidade de cada bloco — reaproveita a paleta já usada em CORES_GRUPO (dashboard).
const BLOCO_COR: Record<Bloco, string> = {
  'RECEITAS OPERACIONAIS': '#63e6be',
  'DEDUÇÕES DA RECEITA': '#e599f7',
  'CUSTOS OPERACIONAIS': '#ff6b6b',
  'DESPESAS OPERACIONAIS': '#ffa94d',
  'ATIVIDADES DE INVESTIMENTO': '#74c0fc',
  'ATIVIDADES DE FINANCIAMENTO': '#a9e34b',
}

const BLOCO_PREFIXO: Record<Bloco, string> = {
  'RECEITAS OPERACIONAIS': '',
  'DEDUÇÕES DA RECEITA': '(-) ',
  'CUSTOS OPERACIONAIS': '(-) ',
  'DESPESAS OPERACIONAIS': '(-) ',
  'ATIVIDADES DE INVESTIMENTO': '(±) ',
  'ATIVIDADES DE FINANCIAMENTO': '(±) ',
}

function periodoParaDatas(tipo: TipoPeriodo, ano: number, mes: number): { de: string; ate: string } {
  if (tipo === 'mes') {
    const ultimo = new Date(ano, mes, 0).getDate()
    return {
      de: `${ano}-${String(mes).padStart(2, '0')}-01`,
      ate: `${ano}-${String(mes).padStart(2, '0')}-${ultimo}`,
    }
  }
  if (tipo === 'trimestre') {
    const q = Math.ceil(mes / 3)
    const mesInicio = (q - 1) * 3 + 1
    const mesFim = q * 3
    const ultimoDia = new Date(ano, mesFim, 0).getDate()
    return {
      de: `${ano}-${String(mesInicio).padStart(2, '0')}-01`,
      ate: `${ano}-${String(mesFim).padStart(2, '0')}-${ultimoDia}`,
    }
  }
  return { de: `${ano}-01-01`, ate: `${ano}-12-31` }
}

function labelPeriodo(tipo: TipoPeriodo, ano: number, mes: number): string {
  if (tipo === 'mes') return `${MESES[mes - 1]}/${ano}`
  if (tipo === 'trimestre') return `T${Math.ceil(mes / 3)}/${ano}`
  return `${ano}`
}

/** Desloca (ano, mes) `deslocamento` períodos (do tipo selecionado) para trás. */
function deslocarPeriodo(tipo: TipoPeriodo, ano: number, mes: number, deslocamento: number): { ano: number; mes: number } {
  if (deslocamento === 0) return { ano, mes }
  if (tipo === 'ano') return { ano: ano - deslocamento, mes }
  const passoMeses = tipo === 'trimestre' ? 3 : 1
  const indiceAbsoluto = ano * 12 + (mes - 1) - deslocamento * passoMeses
  return { ano: Math.floor(indiceAbsoluto / 12), mes: (((indiceAbsoluto % 12) + 12) % 12) + 1 }
}

function fmtPctOuTraco(v: number | null): string {
  return v === null ? '—' : fmtPct(v)
}

/** Largura da barra de proporção — participação absoluta sobre a Receita Bruta, sempre 0–100. */
function larguraBarra(percentual: number | null): number {
  if (percentual === null) return 0
  return Math.min(100, Math.abs(percentual))
}

interface Periodo {
  chave: string
  label: string
  de: string
  ate: string
}

interface Drill {
  periodoLabel: string
  de: string
  ate: string
  categoriaNome: string
}

type LinhaTipo = 'bloco' | 'subtotal'
interface LinhaDre {
  key: string
  tipo: LinhaTipo
  label: string
  bloco?: Bloco
  final?: boolean
}

const LINHAS: LinhaDre[] = [
  { key: 'RECEITAS OPERACIONAIS', tipo: 'bloco', label: 'Receitas Operacionais', bloco: 'RECEITAS OPERACIONAIS' },
  { key: 'DEDUÇÕES DA RECEITA', tipo: 'bloco', label: 'Deduções da Receita', bloco: 'DEDUÇÕES DA RECEITA' },
  { key: 'CUSTOS OPERACIONAIS', tipo: 'bloco', label: 'Custos Operacionais', bloco: 'CUSTOS OPERACIONAIS' },
  { key: 'margemContribuicao', tipo: 'subtotal', label: 'Margem de Contribuição' },
  { key: 'DESPESAS OPERACIONAIS', tipo: 'bloco', label: 'Despesas Operacionais', bloco: 'DESPESAS OPERACIONAIS' },
  { key: 'resultadoOperacional', tipo: 'subtotal', label: 'Resultado Operacional' },
  { key: 'ATIVIDADES DE INVESTIMENTO', tipo: 'bloco', label: 'Atividades de Investimento', bloco: 'ATIVIDADES DE INVESTIMENTO' },
  { key: 'ATIVIDADES DE FINANCIAMENTO', tipo: 'bloco', label: 'Atividades de Financiamento', bloco: 'ATIVIDADES DE FINANCIAMENTO' },
  { key: 'resultadoLiquido', tipo: 'subtotal', label: 'Resultado Líquido', final: true },
]

function valorEPercentualSubtotal(dre: Dre, key: string): { total: number; percentual: number | null } {
  if (key === 'margemContribuicao') return { total: dre.margemContribuicao, percentual: dre.margemContribuicaoPercentual }
  if (key === 'resultadoOperacional') return { total: dre.resultadoOperacional, percentual: dre.resultadoOperacionalPercentual }
  return { total: dre.resultadoLiquido, percentual: dre.resultadoLiquidoPercentual }
}

export default function ClientDrePage() {
  const { user } = useAuth()
  const clienteId = user?.usuarioId ?? ''
  const { registros } = useRegistros(clienteId || null)

  const hoje = new Date()
  const [tipo, setTipo] = useState<TipoPeriodo>('mes')
  const [ano, setAno] = useState(hoje.getFullYear())
  const [mes, setMes] = useState(hoje.getMonth() + 1)
  const [contaFiltro, setContaFiltro] = useState<string>('')
  const [contas, setContas] = useState<ContaBancaria[]>([])
  const [comparar, setComparar] = useState(false)
  const [qtdPeriodos, setQtdPeriodos] = useState(3)

  const [dresPorPeriodo, setDresPorPeriodo] = useState<Record<string, Dre>>({})
  const [loading, setLoading] = useState(false)
  const [erro, setErro] = useState('')
  const [blocosExpandidos, setBlocosExpandidos] = useState<Set<Bloco>>(new Set())
  const [gruposExpandidos, setGruposExpandidos] = useState<Set<string>>(new Set())
  const [drill, setDrill] = useState<Drill | null>(null)

  useEffect(() => {
    if (!clienteId) return
    listarContasBancarias(clienteId)
      .then(cs => setContas(cs.filter(c => c.ativa)))
      .catch(() => {})
  }, [clienteId])

  const periodos = useMemo<Periodo[]>(() => {
    const qtd = comparar ? qtdPeriodos : 1
    return Array.from({ length: qtd }, (_, i) => qtd - 1 - i).map(deslocamento => {
      const { ano: a, mes: m } = deslocarPeriodo(tipo, ano, mes, deslocamento)
      const { de, ate } = periodoParaDatas(tipo, a, m)
      return { chave: `${tipo}-${a}-${m}`, label: labelPeriodo(tipo, a, m), de, ate }
    })
  }, [comparar, qtdPeriodos, tipo, ano, mes])

  const carregar = useCallback(async () => {
    if (!clienteId) return
    setLoading(true)
    setErro('')
    try {
      const resultados = await Promise.all(
        periodos.map(p => obterDre(clienteId, p.de, p.ate, contaFiltro || undefined))
      )
      const mapa: Record<string, Dre> = {}
      periodos.forEach((p, i) => { mapa[p.chave] = resultados[i] })
      setDresPorPeriodo(mapa)
    } catch (e: unknown) {
      setErro(e instanceof Error ? e.message : 'Erro ao carregar DRE.')
    } finally {
      setLoading(false)
    }
  }, [clienteId, periodos, contaFiltro])

  useEffect(() => { carregar() }, [carregar])

  function toggleBloco(bloco: Bloco) {
    setBlocosExpandidos(prev => {
      const next = new Set(prev)
      if (next.has(bloco)) next.delete(bloco)
      else next.add(bloco)
      return next
    })
  }

  function toggleGrupo(chave: string) {
    setGruposExpandidos(prev => {
      const next = new Set(prev)
      if (next.has(chave)) next.delete(chave)
      else next.add(chave)
      return next
    })
  }

  function abrirLancamentos(periodo: Periodo, categoriaNome: string) {
    setDrill({ periodoLabel: periodo.label, de: periodo.de, ate: periodo.ate, categoriaNome })
  }

  const lancamentosDrill = useMemo(() => {
    if (!drill) return []
    const linhas: { data: string; descricao: string; valor: number }[] = []
    for (const r of registros) {
      if (r.data < drill.de || r.data > drill.ate) continue
      if (contaFiltro && r.contaBancariaId !== contaFiltro) continue
      for (const e of r.entradas) {
        const nomeEfetivo = e.categoria && e.categoria.trim() ? e.categoria : 'Não Classificado'
        if (nomeEfetivo === drill.categoriaNome) linhas.push({ data: r.data, descricao: e.descricao, valor: e.valor })
      }
      for (const s of r.saidas) {
        const nomeEfetivo = s.categoria && s.categoria.trim() ? s.categoria : 'Não Classificado'
        if (nomeEfetivo === drill.categoriaNome) linhas.push({ data: r.data, descricao: s.descricao, valor: -s.valor })
      }
    }
    return linhas.sort((a, b) => a.data.localeCompare(b.data))
  }, [drill, registros, contaFiltro])

  const anosOpcoes = Array.from({ length: 5 }, (_, i) => hoje.getFullYear() - 2 + i)
  const trimestresOpcoes = [1, 2, 3, 4]

  const periodosComDre = periodos.map(p => ({ periodo: p, dre: dresPorPeriodo[p.chave] }))
  const todasCarregadas = periodosComDre.every(p => p.dre)
  const modoComparativo = periodos.length > 1

  function encontrarBloco(dre: Dre, bloco: Bloco): DreBloco {
    return dre.blocos.find(b => b.bloco === bloco) ?? { bloco, total: 0, percentual: 0, grupos: [] }
  }

  const naoClassificadoTotal = todasCarregadas
    ? periodosComDre.reduce((acc, p) => {
        const soma = p.dre.blocos
          .flatMap(b => b.grupos)
          .filter(g => g.nome === 'Não Classificado')
          .reduce((s, g) => s + Math.abs(g.total), 0)
        return acc + soma
      }, 0)
    : 0

  return (
    <>
      <div className="dre-header">
        <div>
          <h2 className="dre-titulo">📑 Demonstrativo de Resultado (DRE)</h2>
          <div className="dre-subtitulo">
            Bloco → Grupo → Categoria · % sobre Receita Operacional (base = 100%)
          </div>
        </div>
      </div>

      <div className="dre-controles">
        <div className="dre-tipo-tabs">
          {(['mes', 'trimestre', 'ano'] as TipoPeriodo[]).map(t => (
            <button
              key={t}
              className={`dre-tipo-btn${tipo === t ? ' active' : ''}`}
              onClick={() => setTipo(t)}
            >
              {t === 'mes' ? 'Mês' : t === 'trimestre' ? 'Trimestre' : 'Ano'}
            </button>
          ))}
        </div>

        <div className="dre-filtros">
          <select className="dre-select" value={ano} onChange={e => setAno(+e.target.value)}>
            {anosOpcoes.map(a => <option key={a} value={a}>{a}</option>)}
          </select>

          {tipo === 'mes' && (
            <select className="dre-select" value={mes} onChange={e => setMes(+e.target.value)}>
              {MESES.map((m, i) => <option key={i + 1} value={i + 1}>{m}</option>)}
            </select>
          )}

          {tipo === 'trimestre' && (
            <select className="dre-select" value={Math.ceil(mes / 3)} onChange={e => setMes((+e.target.value - 1) * 3 + 1)}>
              {trimestresOpcoes.map(q => <option key={q} value={q}>T{q}</option>)}
            </select>
          )}

          {contas.length > 1 && (
            <select className="dre-select" value={contaFiltro} onChange={e => setContaFiltro(e.target.value)}>
              <option value="">Todas as contas</option>
              {contas.map(c => <option key={c.id} value={c.id}>{c.nome}</option>)}
            </select>
          )}
        </div>

        <div className="dre-comparar">
          <label className="dre-comparar-toggle">
            <input type="checkbox" checked={comparar} onChange={e => setComparar(e.target.checked)} />
            Comparar períodos
          </label>
          {comparar && (
            <select className="dre-select" value={qtdPeriodos} onChange={e => setQtdPeriodos(+e.target.value)}>
              {[2, 3, 4, 6].map(n => <option key={n} value={n}>Últimos {n}</option>)}
            </select>
          )}
        </div>
      </div>

      {loading && <p className="dre-loading">Calculando...</p>}
      {erro && <p className="dre-erro">{erro}</p>}

      {todasCarregadas && !loading && naoClassificadoTotal > 0 && (
        <div className="dre-alerta-global">
          ⚠ Há lançamentos <strong>Não Classificados</strong> somando {fmtBRL(naoClassificadoTotal)} no período — categorize-os em "Categorizar Lançamentos" para um DRE mais preciso.
        </div>
      )}

      {todasCarregadas && !loading && (
        <div className={`dre-corpo${modoComparativo ? ' dre-corpo-comparativo' : ''}`}>
          {modoComparativo && (
            <div
              className="dre-cmp-cabecalho"
              style={{ gridTemplateColumns: `minmax(200px,1.6fr) repeat(${periodosComDre.length}, minmax(110px,1fr))` }}
            >
              <span />
              {periodosComDre.map(p => <span key={p.periodo.chave} className="dre-cmp-cabecalho-label">{p.periodo.label}</span>)}
            </div>
          )}

          {LINHAS.map(linha => {
            if (linha.tipo === 'subtotal') {
              const valores = periodosComDre.map(p => valorEPercentualSubtotal(p.dre, linha.key))
              const principal = valores[0]
              const corPositivoNegativo = principal.total >= 0 ? 'var(--success)' : 'var(--danger)'
              return (
                <div
                  key={linha.key}
                  className={`dre-linha dre-linha-subtotal${linha.final ? ' dre-linha-final' : ''}${modoComparativo ? ' dre-linha-cmp' : ''}`}
                  style={modoComparativo ? { gridTemplateColumns: `minmax(200px,1.6fr) repeat(${periodosComDre.length}, minmax(110px,1fr))` } : undefined}
                >
                  <span className="dre-linha-label">{linha.label}</span>
                  {!modoComparativo ? (
                    <>
                      <span className="dre-linha-valor" style={linha.final ? { color: corPositivoNegativo } : undefined}>{fmtBRL(principal.total)}</span>
                      <span className="dre-linha-pct">{fmtPctOuTraco(principal.percentual)}</span>
                    </>
                  ) : (
                    valores.map((v, i) => (
                      <span key={periodosComDre[i].periodo.chave} className="dre-cmp-cel">
                        <span className="dre-cmp-cel-valor" style={linha.final ? { color: v.total >= 0 ? 'var(--success)' : 'var(--danger)' } : undefined}>{fmtBRL(v.total)}</span>
                        <span className="dre-cmp-cel-pct">{fmtPctOuTraco(v.percentual)}</span>
                      </span>
                    ))
                  )}
                </div>
              )
            }

            const bloco = linha.bloco!
            const cor = BLOCO_COR[bloco]
            const blocosPorPeriodo = periodosComDre.map(p => encontrarBloco(p.dre, bloco))
            const principal = blocosPorPeriodo[0]
            const expandido = blocosExpandidos.has(bloco)
            const vazio = blocosPorPeriodo.every(b => b.grupos.length === 0)

            return (
              <div key={linha.key} className="dre-bloco-wrap">
                <div
                  className={`dre-linha dre-linha-bloco${modoComparativo ? ' dre-linha-cmp' : ''}${vazio ? ' dre-linha-vazia' : ''}`}
                  style={{
                    borderLeft: `4px solid ${cor}`,
                    ...(modoComparativo ? { gridTemplateColumns: `minmax(200px,1.6fr) repeat(${periodosComDre.length}, minmax(110px,1fr))` } : {}),
                  }}
                  onClick={vazio ? undefined : () => toggleBloco(bloco)}
                  role={vazio ? undefined : 'button'}
                >
                  <span className="dre-linha-label">
                    {!vazio && <span className="dre-expand">{expandido ? '▾' : '▸'}</span>}
                    <span className="dre-bloco-nome" style={{ color: cor }}>{BLOCO_PREFIXO[bloco]}{linha.label.toUpperCase()}</span>
                  </span>
                  {!modoComparativo ? (
                    <>
                      <span className="dre-linha-valor">{fmtBRL(principal.total)}</span>
                      <span className="dre-linha-pct">{fmtPctOuTraco(principal.percentual)}</span>
                    </>
                  ) : (
                    blocosPorPeriodo.map((b, i) => (
                      <span key={periodosComDre[i].periodo.chave} className="dre-cmp-cel">
                        <span className="dre-cmp-cel-valor">{fmtBRL(b.total)}</span>
                        <span className="dre-cmp-cel-pct">{fmtPctOuTraco(b.percentual)}</span>
                      </span>
                    ))
                  )}
                </div>
                <div className="dre-barra-trilho">
                  <div className="dre-barra-preenchida" style={{ width: `${larguraBarra(principal.percentual)}%`, background: cor }} />
                </div>

                {expandido && !vazio && (
                  <div className="dre-expand-bloco">
                    {blocosPorPeriodo.map((b, pIdx) => (
                      <div key={periodosComDre[pIdx].periodo.chave} className="dre-expand-periodo">
                        {modoComparativo && <div className="dre-expand-periodo-label">{periodosComDre[pIdx].periodo.label}</div>}
                        {b.grupos.length === 0 ? (
                          <div className="dre-vazio-inline">Nenhum lançamento neste bloco.</div>
                        ) : (
                          b.grupos.map(grupo => {
                            const chaveGrupo = `${bloco}::${grupo.nome}`
                            const grupoAberto = gruposExpandidos.has(chaveGrupo)
                            return (
                              <div key={grupo.nome} className="dre-grupo-wrap">
                                <button type="button" className="dre-grupo-linha" onClick={() => toggleGrupo(chaveGrupo)}>
                                  <span className="dre-expand dre-expand-grupo">{grupoAberto ? '▾' : '▸'}</span>
                                  <span className="dre-grupo-nome">{grupo.nome}</span>
                                  <span className="dre-grupo-valor">{fmtBRL(grupo.total)}</span>
                                  <span className="dre-grupo-pct">{fmtPctOuTraco(grupo.percentual)}</span>
                                </button>
                                <div className="dre-barra-trilho dre-barra-trilho-grupo">
                                  <div className="dre-barra-preenchida" style={{ width: `${larguraBarra(grupo.percentual)}%`, background: cor, opacity: 0.6 }} />
                                </div>
                                {grupoAberto && (
                                  <div className="dre-cat-lista">
                                    {grupo.categorias.map(cat => (
                                      <button
                                        key={cat.nome}
                                        className={`dre-cat-btn${cat.nome === 'Não Classificado' ? ' dre-cat-btn-alerta' : ''}`}
                                        onClick={() => abrirLancamentos(periodosComDre[pIdx].periodo, cat.nome)}
                                      >
                                        <span className="dre-cat-nome">{cat.nome}</span>
                                        <span className="dre-cat-valor">{fmtBRL(cat.total)}</span>
                                        <span className="dre-cat-pct">{fmtPctOuTraco(cat.percentual)}</span>
                                      </button>
                                    ))}
                                  </div>
                                )}
                              </div>
                            )
                          })
                        )}
                      </div>
                    ))}
                  </div>
                )}
              </div>
            )
          })}
        </div>
      )}

      <Modal
        open={!!drill}
        title={drill ? `${drill.categoriaNome} · ${drill.periodoLabel}` : ''}
        onClose={() => setDrill(null)}
      >
        {lancamentosDrill.length === 0 ? (
          <p className="dre-vazio-inline">Nenhum lançamento encontrado.</p>
        ) : (
          <div className="dre-drill-lista">
            {lancamentosDrill.map((l, i) => (
              <div key={i} className="dre-drill-item">
                <span className="dre-drill-data">{fmtDate(l.data)}</span>
                <span className="dre-drill-desc">{l.descricao}</span>
                <span className="dre-drill-valor" style={{ color: l.valor >= 0 ? 'var(--success)' : 'var(--danger)' }}>{fmtBRL(l.valor)}</span>
              </div>
            ))}
          </div>
        )}
      </Modal>
    </>
  )
}
