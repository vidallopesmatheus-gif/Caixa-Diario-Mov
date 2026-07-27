import { useState, useEffect, useMemo } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import StatCard from '../../components/shared/StatCard'
import { fmtBRL } from '../../utils/format'
import { obterIndicadores } from '../../api/metricas'
import type { IndicadoresDecisao, CategoriaIndicador } from '../../api/metricas'
import { useRegistros } from '../../hooks/useRegistros'
import { leituraMargemDre } from '../../utils/leituras'
import {
  BarChart, Bar, LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip,
  ResponsiveContainer, Cell,
} from 'recharts'
import './ClientGrafico.css'

interface Props { clienteIdOverride?: string }

const MESES_ABREV = ['Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun', 'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez']

function mesLabel(mes: string): string {
  const [ano, m] = mes.split('-')
  return `${MESES_ABREV[parseInt(m, 10) - 1]}/${ano.slice(2)}`
}

function fmtPct(v: number | null): string {
  if (v === null) return '—'
  return `${v.toFixed(1)}%`
}

/** Regressão linear simples (mínimos quadrados) sobre uma série de valores igualmente espaçados. */
function regressaoLinear(valores: number[]): number[] {
  const n = valores.length
  if (n === 0) return []
  if (n === 1) return [valores[0]]
  const somaX = valores.reduce((s: number, _v, i) => s + i, 0)
  const somaY = valores.reduce((s, v) => s + v, 0)
  const somaXY = valores.reduce((s, v, i) => s + v * i, 0)
  const somaX2 = valores.reduce((s: number, _v, i) => s + i * i, 0)
  const denom = n * somaX2 - somaX * somaX
  const slope = denom !== 0 ? (n * somaXY - somaX * somaY) / denom : 0
  const intercept = (somaY - slope * somaX) / n
  return valores.map((_v, i) => slope * i + intercept)
}

export default function ClientGraficoPage({ clienteIdOverride }: Props) {
  const { user } = useAuth()
  const clienteId = clienteIdOverride ?? user?.usuarioId ?? null
  const [indicadores, setIndicadores] = useState<IndicadoresDecisao | null>(null)
  const [loading, setLoading] = useState(true)
  const [erro, setErro] = useState('')
  const [categoriaAberta, setCategoriaAberta] = useState<string | null>(null)
  const { registros } = useRegistros(clienteId)

  useEffect(() => {
    if (!clienteId) return
    setLoading(true)
    obterIndicadores(clienteId)
      .then(setIndicadores)
      .catch(e => setErro(e instanceof Error ? e.message : 'Erro ao carregar indicadores.'))
      .finally(() => setLoading(false))
  }, [clienteId])

  const hoje = new Date()
  const lancamentosPorCategoria = useMemo(() => {
    const anoAtual = hoje.getFullYear()
    const mesAtual = hoje.getMonth() + 1
    const mapa: Record<string, { data: string; descricao: string; valor: number }[]> = {}
    for (const r of registros) {
      const [ano, mes] = r.data.split('-').map(Number)
      if (ano !== anoAtual || mes !== mesAtual) continue
      for (const s of r.saidas) {
        const cat = s.categoria || 'Não Classificado'
        if (!mapa[cat]) mapa[cat] = []
        mapa[cat].push({ data: r.data, descricao: s.descricao, valor: s.valor })
      }
    }
    return mapa
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [registros])

  if (loading) return <p style={{ color: 'var(--tx3)' }}>Carregando...</p>
  if (erro) return <p className="val-red">{erro}</p>
  if (!indicadores) return null

  const { dre } = indicadores
  const ultimos6 = indicadores.evolucao.slice(-6)
  const ultimos12 = indicadores.evolucao.slice(-12)
  const mesesPositivos = ultimos6.filter(m => m.lucro > 0).length

  const custoTotalClassificavel = indicadores.custoFixo + indicadores.custoVariavel + indicadores.custoNaoClassificado
  const pctFixo = custoTotalClassificavel > 0 ? (indicadores.custoFixo / custoTotalClassificavel) * 100 : 0
  const pctVariavel = custoTotalClassificavel > 0 ? (indicadores.custoVariavel / custoTotalClassificavel) * 100 : 0
  const pctNaoClass = custoTotalClassificavel > 0 ? (indicadores.custoNaoClassificado / custoTotalClassificavel) * 100 : 0

  const leituraMargem = leituraMargemDre(dre.margem)

  let leitura6Meses: string
  if (ultimos6.length === 0) {
    leitura6Meses = ''
  } else if (mesesPositivos === ultimos6.length) {
    leitura6Meses = `Lucro positivo em todos os últimos ${ultimos6.length} meses — resultado consistente.`
  } else if (mesesPositivos === 0) {
    leitura6Meses = `Nenhum dos últimos ${ultimos6.length} meses fechou com lucro.`
  } else {
    leitura6Meses = `Lucro positivo em ${mesesPositivos} dos últimos ${ultimos6.length} meses — o resultado ainda não é consistente.`
  }

  const maiorCategoria = indicadores.rankingCategorias[0]
  const categoriaEmAlta = indicadores.rankingCategorias.find(c => (c.variacaoPercentual ?? 0) > 15)
  let leituraCategorias = ''
  if (maiorCategoria) {
    leituraCategorias = `Sua maior despesa este mês é ${maiorCategoria.nome}${maiorCategoria.percentualReceita !== null ? `, respondendo por ${fmtPct(maiorCategoria.percentualReceita)} da receita` : ''}.`
    if (categoriaEmAlta && categoriaEmAlta.nome !== maiorCategoria.nome) {
      leituraCategorias += ` ${categoriaEmAlta.nome} chamou atenção: subiu ${fmtPct(categoriaEmAlta.variacaoPercentual)} em relação à média dos meses anteriores.`
    } else if (categoriaEmAlta && categoriaEmAlta.nome === maiorCategoria.nome) {
      leituraCategorias += ` Ela também subiu ${fmtPct(categoriaEmAlta.variacaoPercentual)} em relação à média dos meses anteriores.`
    }
  } else {
    leituraCategorias = 'Nenhuma despesa lançada neste mês ainda.'
  }

  const historicoSuficiente = indicadores.mesesComAtividade >= 3
  const receitaSerie = ultimos12.map(e => e.receita)
  const primeiroAtivo = receitaSerie.findIndex(v => v > 0)
  const serieParaRegressao = primeiroAtivo >= 0 ? receitaSerie.slice(primeiroAtivo) : []
  const tendenciaValores = regressaoLinear(serieParaRegressao)
  const mediaReceitaSerie = serieParaRegressao.length > 0
    ? serieParaRegressao.reduce((s, v) => s + v, 0) / serieParaRegressao.length
    : 0
  const slope = tendenciaValores.length >= 2 ? tendenciaValores[1] - tendenciaValores[0] : 0
  const limiarSlope = mediaReceitaSerie * 0.02
  const direcao = slope > limiarSlope ? 'alta' : slope < -limiarSlope ? 'queda' : 'estável'

  const dadosGraficoReceita = ultimos12.map((e, i) => ({
    mes: mesLabel(e.mes),
    receita: e.receita,
    tendencia: primeiroAtivo >= 0 && i >= primeiroAtivo ? tendenciaValores[i - primeiroAtivo] : undefined,
  }))

  let leituraTendencia = `Sua receita está em tendência de ${direcao} nos meses com movimento.`
  if (indicadores.variacaoReceitaMesAnterior !== null) {
    const v = indicadores.variacaoReceitaMesAnterior
    leituraTendencia += ` Este mês está ${v >= 0 ? `${v.toFixed(1)}% maior` : `${Math.abs(v).toFixed(1)}% menor`} que o mês passado.`
  }
  if (indicadores.variacaoReceitaAnoAnterior !== null) {
    const v = indicadores.variacaoReceitaAnoAnterior
    leituraTendencia += ` Comparado ao mesmo mês do ano passado, está ${v >= 0 ? `${v.toFixed(1)}% maior` : `${Math.abs(v).toFixed(1)}% menor`}.`
  }

  function toggleCategoria(nome: string) {
    setCategoriaAberta(prev => (prev === nome ? null : nome))
  }

  function classeVariacao(c: CategoriaIndicador): string {
    if (c.variacaoPercentual === null) return ''
    if (c.variacaoPercentual > 15) return 'val-red'
    if (c.variacaoPercentual < -15) return 'val-green'
    return ''
  }

  function textoVariacao(c: CategoriaIndicador): string {
    if (c.variacaoPercentual === null) return 'sem histórico anterior'
    if (c.variacaoPercentual > 15) return `↑ subiu ${fmtPct(c.variacaoPercentual)} vs. meses anteriores`
    if (c.variacaoPercentual < -15) return `↓ caiu ${fmtPct(Math.abs(c.variacaoPercentual))} vs. meses anteriores`
    return 'estável vs. meses anteriores'
  }

  return (
    <>
      {/* BLOCO 1 */}
      <section className="ind-bloco">
        <h2 className="ind-pergunta">Meu negócio dá lucro de verdade?</h2>

        <div className="stats-grid">
          <StatCard label="Receita do mês" value={fmtBRL(dre.receitaBruta)} className="val-green" />
          <StatCard label="Custos e despesas" value={fmtBRL(dre.totalDespesas)} className="val-red" />
          <StatCard label="Resultado" value={fmtBRL(dre.resultado)} className={dre.resultado >= 0 ? 'val-green' : 'val-red'} />
        </div>

        <div className="ind-margem-card">
          <div className="ind-margem-valor">{dre.margem !== null ? `${dre.margem.toFixed(1)}%` : '—'}</div>
          <div className="ind-margem-leitura">{leituraMargem}</div>
        </div>

        <h4 className="ind-subtitulo">Estrutural (fixo) x pontual (variável)</h4>
        {custoTotalClassificavel === 0 ? (
          <p className="ind-vazio">Nenhuma saída lançada neste mês ainda.</p>
        ) : (
          <>
            <div className="ind-barra-fixo-variavel">
              {indicadores.custoFixo > 0 && <div className="ind-barra-seg ind-seg-fixo" style={{ width: `${pctFixo}%` }} />}
              {indicadores.custoVariavel > 0 && <div className="ind-barra-seg ind-seg-variavel" style={{ width: `${pctVariavel}%` }} />}
              {indicadores.custoNaoClassificado > 0 && <div className="ind-barra-seg ind-seg-nao-class" style={{ width: `${pctNaoClass}%` }} />}
            </div>
            <div className="ind-legenda-fixo-variavel">
              <span><i className="ind-dot ind-seg-fixo" /> Fixo: {fmtBRL(indicadores.custoFixo)}</span>
              <span><i className="ind-dot ind-seg-variavel" /> Variável: {fmtBRL(indicadores.custoVariavel)}</span>
              {indicadores.custoNaoClassificado > 0 && (
                <span><i className="ind-dot ind-seg-nao-class" /> Não classificado: {fmtBRL(indicadores.custoNaoClassificado)}</span>
              )}
            </div>
          </>
        )}

        <h4 className="ind-subtitulo">Últimos {ultimos6.length} meses</h4>
        <div className="ind-chart-card" style={{ height: 180 }}>
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={ultimos6.map(e => ({ mes: mesLabel(e.mes), lucro: e.lucro }))}>
              <CartesianGrid strokeDasharray="3 3" stroke="var(--bd)" />
              <XAxis dataKey="mes" stroke="var(--tx3)" tick={{ fontSize: 12 }} />
              <YAxis stroke="var(--tx3)" tick={{ fontSize: 12 }} tickFormatter={v => `R$${(v / 1000).toFixed(0)}k`} />
              <Tooltip formatter={v => typeof v === 'number' ? fmtBRL(v) : String(v)} contentStyle={{ background: 'var(--bg-card)', border: '1px solid var(--bd)' }} />
              <Bar dataKey="lucro" name="Lucro" radius={[4, 4, 0, 0]}>
                {ultimos6.map((e, i) => <Cell key={i} fill={e.lucro >= 0 ? '#34c759' : '#ff3b30'} />)}
              </Bar>
            </BarChart>
          </ResponsiveContainer>
        </div>
        <p className="ind-leitura">{leitura6Meses}</p>
      </section>

      {/* BLOCO 2 */}
      <section className="ind-bloco">
        <h2 className="ind-pergunta">Qual categoria mais me custa?</h2>
        <p className="ind-leitura">{leituraCategorias}</p>

        {indicadores.rankingCategorias.length === 0 ? (
          <p className="ind-vazio">Sem despesas no período.</p>
        ) : (
          <div className="ind-ranking">
            {indicadores.rankingCategorias.map(c => (
              <div key={c.nome} className="ind-ranking-item">
                <div className="ind-ranking-linha" onClick={() => toggleCategoria(c.nome)}>
                  <span className="ind-ranking-nome">{c.nome}</span>
                  <span className="ind-ranking-valor">{fmtBRL(c.total)}</span>
                  <span className="ind-ranking-pct">{fmtPct(c.percentualReceita)} da receita</span>
                  <span className={`ind-ranking-variacao ${classeVariacao(c)}`}>{textoVariacao(c)}</span>
                </div>
                {categoriaAberta === c.nome && (
                  <div className="ind-lancamentos">
                    {(lancamentosPorCategoria[c.nome] ?? []).length === 0 ? (
                      <p className="ind-vazio">Nenhum lançamento encontrado neste mês.</p>
                    ) : (
                      (lancamentosPorCategoria[c.nome] ?? []).map((l, i) => (
                        <div key={i} className="ind-lancamento-item">
                          <span>{l.data.slice(8, 10)}/{l.data.slice(5, 7)}</span>
                          <span>{l.descricao}</span>
                          <span>{fmtBRL(l.valor)}</span>
                        </div>
                      ))
                    )}
                  </div>
                )}
              </div>
            ))}
          </div>
        )}
      </section>

      {/* BLOCO 3 */}
      <section className="ind-bloco">
        <h2 className="ind-pergunta">Estou crescendo ou estagnado?</h2>

        {!historicoSuficiente ? (
          <p className="ind-vazio">
            Ainda não há histórico suficiente (menos de 3 meses de movimentação) para avaliar se o negócio está
            crescendo. Continue lançando seus dados e essa análise vai ficar mais precisa.
          </p>
        ) : (
          <>
            <div className="ind-chart-card" style={{ height: 220 }}>
              <ResponsiveContainer width="100%" height="100%">
                <LineChart data={dadosGraficoReceita}>
                  <CartesianGrid strokeDasharray="3 3" stroke="var(--bd)" />
                  <XAxis dataKey="mes" stroke="var(--tx3)" tick={{ fontSize: 12 }} />
                  <YAxis stroke="var(--tx3)" tick={{ fontSize: 12 }} tickFormatter={v => `R$${(v / 1000).toFixed(0)}k`} />
                  <Tooltip formatter={v => typeof v === 'number' ? fmtBRL(v) : String(v)} contentStyle={{ background: 'var(--bg-card)', border: '1px solid var(--bd)' }} />
                  <Line type="monotone" dataKey="receita" name="Receita" stroke="#0a84ff" strokeWidth={2} dot={{ fill: '#0a84ff', r: 3 }} />
                  <Line type="linear" dataKey="tendencia" name="Tendência" stroke="var(--tx3)" strokeWidth={1.5} strokeDasharray="4 4" dot={false} />
                </LineChart>
              </ResponsiveContainer>
            </div>
            <p className="ind-leitura">{leituraTendencia}</p>
          </>
        )}
      </section>
    </>
  )
}
