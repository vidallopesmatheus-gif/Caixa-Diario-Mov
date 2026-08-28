import { useState, useEffect, useCallback, useMemo } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { obterDre } from '../../api/metricas'
import { listarContasBancarias } from '../../api/contasBancarias'
import { useRegistros } from '../../hooks/useRegistros'
import { fmtBRL, fmtDate } from '../../utils/format'
import Modal from '../../components/shared/Modal'
import type { Dre, DreCategoria } from '../../api/metricas'
import type { ContaBancaria } from '../../types'
import './ClientDre.css'

type TipoPeriodo = 'mes' | 'trimestre' | 'ano'
type BucketKey = 'deducoes' | 'custosVariaveis' | 'despesasFixas' | 'receitaFinanceira' | 'despesasNaoOperacionais' | 'naoClassificado'

const MESES = ['Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun', 'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez']

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

function fmtPct(v: number | null): string {
  if (v === null) return '—'
  return `${v.toLocaleString('pt-BR', { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%`
}

interface LinhaWaterfall {
  key: string
  label: string
  tipo: 'plain' | 'bucket' | 'subtotal'
  total: number
  percentual: number | null
  categorias?: DreCategoria[]
}

function linhasWaterfall(dre: Dre): LinhaWaterfall[] {
  return [
    { key: 'receitaBruta', label: 'RECEITA BRUTA', tipo: 'plain', total: dre.receitaBruta, percentual: dre.receitaBrutaPercentual },
    { key: 'deducoes', label: '(-) Deduções/Impostos', tipo: 'bucket', total: dre.deducoes.total, percentual: dre.deducoes.percentual, categorias: dre.deducoes.categorias },
    { key: 'receitaLiquida', label: '= RECEITA LÍQUIDA', tipo: 'subtotal', total: dre.receitaLiquida, percentual: dre.receitaLiquidaPercentual },
    { key: 'custosVariaveis', label: '(-) Custos Variáveis', tipo: 'bucket', total: dre.custosVariaveis.total, percentual: dre.custosVariaveis.percentual, categorias: dre.custosVariaveis.categorias },
    { key: 'margemContribuicao', label: '= MARGEM DE CONTRIBUIÇÃO', tipo: 'subtotal', total: dre.margemContribuicao, percentual: dre.margemContribuicaoPercentual },
    { key: 'despesasFixas', label: '(-) Despesas Fixas', tipo: 'bucket', total: dre.despesasFixas.total, percentual: dre.despesasFixas.percentual, categorias: dre.despesasFixas.categorias },
    { key: 'resultadoOperacional', label: '= RESULTADO OPERACIONAL', tipo: 'subtotal', total: dre.resultadoOperacional, percentual: dre.resultadoOperacionalPercentual },
    { key: 'receitaFinanceira', label: '(+) Receita Financeira (rendimento)', tipo: 'bucket', total: dre.receitaFinanceira.total, percentual: dre.receitaFinanceira.percentual, categorias: dre.receitaFinanceira.categorias },
    { key: 'despesasNaoOperacionais', label: '(-) Despesas Não Operacionais', tipo: 'bucket', total: dre.despesasNaoOperacionais.total, percentual: dre.despesasNaoOperacionais.percentual, categorias: dre.despesasNaoOperacionais.categorias },
    { key: 'naoClassificado', label: '(-) Não Classificado', tipo: 'bucket', total: dre.naoClassificado.total, percentual: dre.naoClassificado.percentual, categorias: dre.naoClassificado.categorias },
    { key: 'resultadoLiquido', label: '= RESULTADO LÍQUIDO', tipo: 'subtotal', total: dre.resultadoLiquido, percentual: dre.resultadoLiquidoPercentual },
  ]
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
  const [expanded, setExpanded] = useState<Record<BucketKey, boolean>>({
    deducoes: false, custosVariaveis: false, despesasFixas: false, receitaFinanceira: false, despesasNaoOperacionais: false, naoClassificado: false,
  })
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

  function toggleBucket(key: BucketKey) {
    setExpanded(prev => ({ ...prev, [key]: !prev[key] }))
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
      // A Receita Financeira é a única linha do lado das entradas (rendimento) — as demais
      // continuam vindo só das saídas, como sempre.
      for (const e of r.entradas) {
        const nomeEfetivo = e.categoria && e.categoria.trim() ? e.categoria : 'Não Classificado'
        if (nomeEfetivo === drill.categoriaNome) linhas.push({ data: r.data, descricao: e.descricao, valor: e.valor })
      }
      for (const s of r.saidas) {
        const nomeEfetivo = s.categoria && s.categoria.trim() ? s.categoria : 'Não Classificado'
        if (nomeEfetivo === drill.categoriaNome) linhas.push({ data: r.data, descricao: s.descricao, valor: s.valor })
      }
    }
    return linhas.sort((a, b) => a.data.localeCompare(b.data))
  }, [drill, registros, contaFiltro])

  const anosOpcoes = Array.from({ length: 5 }, (_, i) => hoje.getFullYear() - 2 + i)
  const trimestresOpcoes = [1, 2, 3, 4]

  const periodosComDre = periodos.map(p => ({ periodo: p, dre: dresPorPeriodo[p.chave] }))
  const todasCarregadas = periodosComDre.every(p => p.dre)
  const modoComparativo = periodos.length > 1

  return (
    <>
      <div className="dre-header">
        <div>
          <h2 className="dre-titulo">📑 DRE — Análise Vertical</h2>
          <div className="dre-subtitulo">
            Demonstrativo de Resultado · % sobre Receita Bruta (base = 100%)
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

      {todasCarregadas && !loading && (
        <div className={`dre-corpo${modoComparativo ? ' dre-corpo-comparativo' : ''}`}>
          {modoComparativo && (
            <div
              className="dre-cmp-cabecalho"
              style={{ gridTemplateColumns: `minmax(180px,1.4fr) repeat(${periodosComDre.length}, minmax(110px,1fr))` }}
            >
              <span />
              {periodosComDre.map(p => <span key={p.periodo.chave} className="dre-cmp-cabecalho-label">{p.periodo.label}</span>)}
            </div>
          )}

          {linhasWaterfall(periodosComDre[0].dre).map((linhaBase, idx) => {
            const bucketKey = linhaBase.tipo === 'bucket' ? (linhaBase.key as BucketKey) : null
            const linhasPorPeriodo = periodosComDre.map(p => linhasWaterfall(p.dre)[idx])
            const alertaNaoClassificado = bucketKey === 'naoClassificado' && linhaBase.total > 0

            return (
              <div key={linhaBase.key}>
                <div
                  className={[
                    'dre-linha',
                    linhaBase.tipo === 'plain' ? 'dre-linha-receita' : '',
                    linhaBase.tipo === 'subtotal' ? 'dre-linha-subtotal' : '',
                    bucketKey ? 'dre-linha-bucket' : '',
                    alertaNaoClassificado ? 'dre-linha-alerta' : '',
                    modoComparativo ? 'dre-linha-cmp' : '',
                  ].filter(Boolean).join(' ')}
                  style={modoComparativo ? { gridTemplateColumns: `minmax(180px,1.4fr) repeat(${periodosComDre.length}, minmax(110px,1fr))` } : undefined}
                  onClick={bucketKey ? () => toggleBucket(bucketKey) : undefined}
                  role={bucketKey ? 'button' : undefined}
                  data-final={linhaBase.key === 'resultadoLiquido' ? 'true' : undefined}
                >
                  <span className="dre-linha-label">
                    {bucketKey && <span className="dre-expand">{expanded[bucketKey] ? '▾' : '▸'}</span>}
                    {linhaBase.label}
                    {alertaNaoClassificado && <span className="dre-naoclass-badge">verifique</span>}
                  </span>

                  {!modoComparativo ? (
                    <span className="dre-linha-valor">
                      {fmtBRL(linhaBase.total)}
                      <span className="dre-linha-pct">{fmtPct(linhaBase.percentual)}</span>
                    </span>
                  ) : (
                    linhasPorPeriodo.map((l, i) => (
                      <span key={periodosComDre[i].periodo.chave} className="dre-cmp-cel">
                        <span className="dre-cmp-cel-valor">{fmtBRL(l.total)}</span>
                        <span className="dre-cmp-cel-pct">{fmtPct(l.percentual)}</span>
                      </span>
                    ))
                  )}
                </div>

                {bucketKey && expanded[bucketKey] && (
                  <div className="dre-expand-bloco">
                    {periodosComDre.map((p, i) => {
                      const cats = linhasPorPeriodo[i].categorias ?? []
                      return (
                        <div key={p.periodo.chave} className="dre-expand-periodo">
                          {modoComparativo && <div className="dre-expand-periodo-label">{p.periodo.label}</div>}
                          {cats.length === 0 ? (
                            <div className="dre-vazio-inline">Nenhum lançamento nesta linha.</div>
                          ) : (
                            cats.map(cat => (
                              <button
                                key={cat.nome}
                                className="dre-cat-btn"
                                onClick={() => abrirLancamentos(p.periodo, cat.nome)}
                              >
                                <span className="dre-cat-nome">{cat.nome}</span>
                                <span className="dre-cat-valor">{fmtBRL(cat.total)}</span>
                                <span className="dre-cat-pct">{fmtPct(cat.percentual)}</span>
                              </button>
                            ))
                          )}
                        </div>
                      )
                    })}
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
                <span className="dre-drill-valor">{fmtBRL(l.valor)}</span>
              </div>
            ))}
          </div>
        )}
      </Modal>
    </>
  )
}
