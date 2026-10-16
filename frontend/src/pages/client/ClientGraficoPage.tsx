import { useState, useEffect, useMemo, useRef } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { fmtBRL, fmtBRLCompacto, fmtPct as fmtPctBase, fmtNumDecimal } from '../../utils/format'
import { obterIndicadores } from '../../api/metricas'
import type { IndicadoresDecisao, CategoriaIndicador } from '../../api/metricas'
import { useRegistros } from '../../hooks/useRegistros'
import { leituraMargemDre, leituraPontoEquilibrio, leituraFolegoCaixa, leituraPrazoRecebimento } from '../../utils/leituras'
import { MvPage, MvKpi, MvKpiGrid, MvInsight, MvGauge, prefereMovimentoReduzido, type MvTone } from '../../components/mv'
import {
  AreaChart, Area, Line, BarChart, Bar, PieChart, Pie, CartesianGrid, XAxis, YAxis, Tooltip,
  ResponsiveContainer, Cell,
} from 'recharts'
import './ClientGrafico.css'

interface Props { clienteIdOverride?: string }

const MESES_ABREV = ['Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun', 'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez']
const DONUT_CORES = ['var(--mv-blue-light)', 'var(--danger)', 'var(--warning)', 'var(--success)', 'var(--mv-blue)', 'var(--tx3)']

function mesLabel(mes: string): string {
  const [ano, m] = mes.split('-')
  return `${MESES_ABREV[parseInt(m, 10) - 1]}/${ano.slice(2)}`
}

function fmtPct(v: number | null): string {
  return v === null ? '—' : fmtPctBase(v)
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
  // Item 4 (Parte 2, Money Vision): limita o ranking a 10 itens, o resto some em "Outras (N)".
  const [outrasAberta, setOutrasAberta] = useState(false)
  const { registros } = useRegistros(clienteId)

  // Item 10 do Bloco 5 ("Visão de Futuro"): mesmo fallback já usado na DRE (Item 1.11) — se o mês
  // atual ainda não tem nenhum lançamento, usa o último mês com dados em vez de mostrar tudo
  // zerado. Roda só uma vez, quando os registros terminam de carregar.
  const [mesRefAjustado, setMesRefAjustado] = useState<string | null>(null)
  const ajusteInicialRef = useRef(false)
  useEffect(() => {
    if (ajusteInicialRef.current) return
    if (registros.length === 0) return
    ajusteInicialRef.current = true

    const hojeAjuste = new Date()
    const anoHoje = hojeAjuste.getFullYear()
    const mesHoje = hojeAjuste.getMonth() + 1
    const temAtividadeNoMesAtual = registros.some(r => {
      const [a, m] = r.data.split('-').map(Number)
      return a === anoHoje && m === mesHoje
    })
    if (temAtividadeNoMesAtual) return

    const maisRecente = registros.reduce((max, r) => (r.data > max ? r.data : max), registros[0].data)
    setMesRefAjustado(maisRecente.slice(0, 7))
  }, [registros])

  useEffect(() => {
    if (!clienteId) return
    setLoading(true)
    obterIndicadores(clienteId, 13, mesRefAjustado ?? undefined)
      .then(setIndicadores)
      .catch(e => setErro(e instanceof Error ? e.message : 'Erro ao carregar indicadores.'))
      .finally(() => setLoading(false))
  }, [clienteId, mesRefAjustado])

  const hoje = new Date()
  const lancamentosPorCategoria = useMemo(() => {
    let anoAtual = hoje.getFullYear()
    let mesAtual = hoje.getMonth() + 1
    if (mesRefAjustado) {
      const [a, m] = mesRefAjustado.split('-').map(Number)
      anoAtual = a; mesAtual = m
    }
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
  }, [registros, mesRefAjustado])

  if (loading) return <p style={{ color: 'var(--tx3)' }}>Carregando...</p>
  if (erro) return <p className="val-red">{erro}</p>
  if (!indicadores) return null

  const semAnimacao = prefereMovimentoReduzido()
  const { dre, pontoEquilibrio, folegoCaixa, prazoRecebimento } = indicadores
  const ultimos6 = indicadores.evolucao.slice(-6)
  const ultimos12 = indicadores.evolucao.slice(-12)
  const mesesPositivos = ultimos6.filter(m => m.lucro > 0).length

  const custoTotalClassificavel = indicadores.custoFixo + indicadores.custoVariavel + indicadores.custoNaoClassificado
  const pctFixo = custoTotalClassificavel > 0 ? (indicadores.custoFixo / custoTotalClassificavel) * 100 : 0
  const pctVariavel = custoTotalClassificavel > 0 ? (indicadores.custoVariavel / custoTotalClassificavel) * 100 : 0
  const pctNaoClass = custoTotalClassificavel > 0 ? (indicadores.custoNaoClassificado / custoTotalClassificavel) * 100 : 0

  const leituraMargem = leituraMargemDre(dre.margem)

  let leitura6Meses: string
  let toneLeitura6Meses: MvTone = 'neutral'
  if (ultimos6.length === 0) {
    leitura6Meses = ''
  } else if (mesesPositivos === ultimos6.length) {
    leitura6Meses = `Lucro positivo em todos os últimos ${ultimos6.length} meses — resultado consistente.`
    toneLeitura6Meses = 'good'
  } else if (mesesPositivos === 0) {
    leitura6Meses = `Nenhum dos últimos ${ultimos6.length} meses fechou com lucro.`
    toneLeitura6Meses = 'bad'
  } else {
    leitura6Meses = `Lucro positivo em ${mesesPositivos} dos últimos ${ultimos6.length} meses — o resultado ainda não é consistente.`
    toneLeitura6Meses = 'warn'
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

  const rankingTop10 = indicadores.rankingCategorias.slice(0, 10)
  const rankingOutras = indicadores.rankingCategorias.slice(10)
  const outrasTotal = rankingOutras.reduce((s, c) => s + c.total, 0)
  const outrasPercentualReceita = dre.receitaBruta > 0 ? Math.round((outrasTotal / dre.receitaBruta) * 1000) / 10 : null
  const maiorValorRanking = Math.max(...indicadores.rankingCategorias.map(c => c.total), 0)

  // Redesign visual (Money Vision): rosca radial com as top-5 categorias + "Outras" agregada,
  // total no centro — a lista de ranking (abaixo) continua com o detalhe linha a linha.
  const donutTop5 = indicadores.rankingCategorias.slice(0, 5)
  const donutOutrasTotal = indicadores.rankingCategorias.slice(5).reduce((s, c) => s + c.total, 0)
  const donutData = [
    ...donutTop5.map(c => ({ name: c.nome, value: c.total })),
    ...(donutOutrasTotal > 0 ? [{ name: 'Outras', value: donutOutrasTotal }] : []),
  ]
  const donutTotal = donutData.reduce((s, d) => s + d.value, 0)

  const historicoSuficiente = indicadores.mesesComAtividade >= 3
  // Item 1.8: igual ao Bloco 6 (custoFixoSerieAtiva) — um mês sem nenhum lançamento ainda (não
  // "faturamento zero" de verdade) entrava na série como um mergulho a zero, distorcendo a
  // tendência e fazendo o gráfico parecer "pular"/despencar num mês em que só faltava lançar.
  const ultimos12Ativos = ultimos12.filter(e => e.receita > 0 || e.custos > 0)
  const receitaSerie = ultimos12Ativos.map(e => e.receita)
  const primeiroAtivo = receitaSerie.findIndex(v => v > 0)
  const serieParaRegressao = primeiroAtivo >= 0 ? receitaSerie.slice(primeiroAtivo) : []
  const tendenciaValores = regressaoLinear(serieParaRegressao)
  const mediaReceitaSerie = serieParaRegressao.length > 0
    ? serieParaRegressao.reduce((s, v) => s + v, 0) / serieParaRegressao.length
    : 0
  const slope = tendenciaValores.length >= 2 ? tendenciaValores[1] - tendenciaValores[0] : 0
  const limiarSlope = mediaReceitaSerie * 0.02
  const direcao = slope > limiarSlope ? 'alta' : slope < -limiarSlope ? 'queda' : 'estável'

  const dadosGraficoReceita = ultimos12Ativos.map((e, i) => ({
    mes: mesLabel(e.mes),
    receita: e.receita,
    tendencia: primeiroAtivo >= 0 && i >= primeiroAtivo ? tendenciaValores[i - primeiroAtivo] : undefined,
  }))

  let leituraTendencia = `Sua receita está em tendência de ${direcao} nos meses com movimento.`
  if (indicadores.variacaoReceitaMesAnterior !== null) {
    const v = indicadores.variacaoReceitaMesAnterior
    leituraTendencia += ` Este mês está ${v >= 0 ? `${fmtPctBase(v)} maior` : `${fmtPctBase(Math.abs(v))} menor`} que o mês passado.`
  }
  if (indicadores.variacaoReceitaAnoAnterior !== null) {
    const v = indicadores.variacaoReceitaAnoAnterior
    leituraTendencia += ` Comparado ao mesmo mês do ano passado, está ${v >= 0 ? `${fmtPctBase(v)} maior` : `${fmtPctBase(Math.abs(v))} menor`}.`
  }

  // Custo Fixo ÷ Receita: tendência dos últimos 6 meses — o sinal acionável é custo fixo subindo
  // enquanto a receita fica estável ou cai (padrão que antecede aperto de caixa), não o valor isolado.
  const custoFixoSerieAtiva = indicadores.custoFixoMensal.filter(m => m.receita > 0 || m.custoFixo > 0)
  const custoFixoHistoricoSuficiente = custoFixoSerieAtiva.length >= 3

  function direcaoSerie(valores: number[]): 'subindo' | 'caindo' | 'estável' {
    const tendencia = regressaoLinear(valores)
    if (tendencia.length < 2) return 'estável'
    const slope = tendencia[tendencia.length - 1] - tendencia[0]
    const media = valores.reduce((s, v) => s + v, 0) / valores.length
    const limiar = Math.abs(media) * 0.05
    return slope > limiar ? 'subindo' : slope < -limiar ? 'caindo' : 'estável'
  }

  const direcaoCustoFixo = custoFixoHistoricoSuficiente ? direcaoSerie(custoFixoSerieAtiva.map(m => m.custoFixo)) : 'estável'
  const direcaoReceitaCF = custoFixoHistoricoSuficiente ? direcaoSerie(custoFixoSerieAtiva.map(m => m.receita)) : 'estável'
  const mesAtualCustoFixo = custoFixoSerieAtiva[custoFixoSerieAtiva.length - 1]

  let leituraCustoFixo = ''
  let toneLeituraCustoFixo: MvTone = 'neutral'
  if (custoFixoHistoricoSuficiente) {
    if (direcaoCustoFixo === 'subindo' && direcaoReceitaCF !== 'subindo') {
      leituraCustoFixo = 'Seu custo fixo está subindo enquanto a receita fica estável ou cai — esse é o padrão que costuma anteceder aperto de caixa. Cautela antes de assumir um custo fixo novo.'
      toneLeituraCustoFixo = 'bad'
    } else if (direcaoCustoFixo === 'subindo') {
      leituraCustoFixo = 'Seu custo fixo está subindo, mas a receita está acompanhando o crescimento.'
      toneLeituraCustoFixo = 'warn'
    } else if (direcaoCustoFixo === 'caindo') {
      leituraCustoFixo = 'Seu custo fixo está em queda nos últimos meses.'
      toneLeituraCustoFixo = 'good'
    } else {
      leituraCustoFixo = 'Seu custo fixo está estável em relação aos últimos meses.'
    }
  }

  function toggleCategoria(nome: string) {
    setCategoriaAberta(prev => (prev === nome ? null : nome))
  }

  // Item 2 (Parte 2, Money Vision): badge de tendência em vez de frase solta — mesmo limiar de
  // ±15% já usado antes, só muda a apresentação (seta + %, "estável" ou "novo").
  function badgeVariacao(c: CategoriaIndicador): { texto: string; classe: string } {
    if (c.variacaoPercentual === null) return { texto: 'novo', classe: 'ind-badge-novo' }
    if (c.variacaoPercentual > 15) return { texto: `▲ ${fmtPct(c.variacaoPercentual)}`, classe: 'ind-badge-alta' }
    if (c.variacaoPercentual < -15) return { texto: `▼ ${fmtPct(Math.abs(c.variacaoPercentual))}`, classe: 'ind-badge-baixa' }
    return { texto: 'estável', classe: 'ind-badge-estavel' }
  }

  // Bloco 4 — barra previsto (receita atual) x marcador (ponto de equilíbrio).
  const peMax = pontoEquilibrio.disponivel
    ? Math.max(pontoEquilibrio.receitaAtual, pontoEquilibrio.valorMensal ?? 0, 1) * 1.15
    : 1
  const peAcimaDoEquilibrio = (pontoEquilibrio.distancia ?? 0) >= 0

  // Bloco 5 — fôlego de caixa como barra 0-12 meses.
  const corFolego = folegoCaixa.faixa === 'critico' ? 'var(--danger)' : folegoCaixa.faixa === 'atencao' ? 'var(--warning)' : 'var(--success)'

  return (
    <MvPage>
      {mesRefAjustado && (
        <p className="ind-vazio" style={{ marginBottom: 16 }}>
          📅 O mês atual ainda não tem lançamentos — mostrando {mesLabel(mesRefAjustado)}, o último mês com movimento.
        </p>
      )}

      {/* BLOCO 1 — Lucro de verdade */}
      <section className="mv-answer-card">
        <h2 className="mv-answer-pergunta">Meu negócio dá lucro de verdade?</h2>

        <div className="mv-gauge-wrap">
          <MvGauge
            value={dre.margem ?? 0}
            min={-20} max={40}
            zonas={[{ ate: 0, cor: 'var(--danger)' }, { ate: 10, cor: 'var(--warning)' }, { ate: 40, cor: 'var(--success)' }]}
            valorFormatado={fmtPct(dre.margem)}
            legenda="margem líquida"
          />
        </div>
        <p style={{ textAlign: 'center', fontSize: 14, color: 'var(--tx2)', margin: '0 0 18px' }}>
          De cada <strong style={{ color: 'var(--tx1)' }}>R$ 100</strong> que entram, sobram{' '}
          <strong style={{ color: (dre.margem ?? 0) >= 0 ? 'var(--success)' : 'var(--danger)' }}>{fmtBRL(dre.margem ?? 0)}</strong>.
        </p>

        <MvKpiGrid>
          <MvKpi label="Receita do mês" value={dre.receitaBruta} tone="neutral" />
          <MvKpi label="Custos e despesas" value={dre.totalDespesas} tone="bad" />
          <MvKpi label="Resultado" value={dre.resultado} tone={dre.resultado >= 0 ? 'good' : 'bad'} />
        </MvKpiGrid>
        <MvInsight text={leituraMargem} tone="neutral" />

        <h4 className="ind-subtitulo">Estrutural (fixo) × pontual (variável)</h4>
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
        <div className="mv-chart-card" style={{ height: 180 }}>
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={ultimos6.map(e => ({ mes: mesLabel(e.mes), lucro: e.lucro }))}>
              <CartesianGrid stroke="var(--tx2)" strokeOpacity={0.12} vertical={false} />
              <XAxis dataKey="mes" tick={{ fontSize: 11, fill: 'var(--tx2)' }} axisLine={{ stroke: 'var(--tx2)', strokeOpacity: 0.3 }} tickLine={false} />
              <YAxis tick={{ fontSize: 11, fill: 'var(--tx3)' }} tickFormatter={v => fmtBRLCompacto(v)} width={60} axisLine={false} tickLine={false} />
              <Tooltip
                formatter={v => typeof v === 'number' ? fmtBRL(v) : String(v)}
                contentStyle={{ background: 'var(--bg-card)', border: '1px solid var(--bd)', borderRadius: 10 }}
                labelStyle={{ color: 'var(--tx2)' }}
              />
              <Bar dataKey="lucro" name="Lucro" radius={[4, 4, 0, 0]} isAnimationActive={!semAnimacao}>
                {ultimos6.map((e, i) => <Cell key={i} fill={e.lucro >= 0 ? 'var(--success)' : 'var(--danger)'} />)}
              </Bar>
            </BarChart>
          </ResponsiveContainer>
        </div>
        <MvInsight text={leitura6Meses} tone={toneLeitura6Meses} />
      </section>

      {/* BLOCO 2 — Categoria que mais custa */}
      <section className="mv-answer-card">
        <h2 className="mv-answer-pergunta">Qual categoria mais me custa?</h2>
        <MvInsight text={leituraCategorias} tone="neutral" />

        {indicadores.rankingCategorias.length === 0 ? (
          <p className="ind-vazio">Sem despesas no período.</p>
        ) : (
          <>
            <div className="mv-donut-wrap">
              <ResponsiveContainer width="100%" height="100%">
                <PieChart>
                  <Pie data={donutData} dataKey="value" nameKey="name" innerRadius="62%" outerRadius="92%" paddingAngle={2} stroke="none" isAnimationActive={!semAnimacao}>
                    {donutData.map((d, i) => <Cell key={d.name} fill={DONUT_CORES[i % DONUT_CORES.length]} />)}
                  </Pie>
                  <Tooltip
                    formatter={v => typeof v === 'number' ? fmtBRL(v) : String(v)}
                    contentStyle={{ background: 'var(--bg-card)', border: '1px solid var(--bd)', borderRadius: 10 }}
                  />
                </PieChart>
              </ResponsiveContainer>
              <div className="mv-donut-centro">
                <div className="mv-donut-centro-valor">{fmtBRLCompacto(donutTotal)}</div>
                <div className="mv-donut-centro-label">total gasto</div>
              </div>
            </div>

            <div className="ind-ranking">
              <div className="ind-ranking-cabecalho">
                <span>Categoria</span>
                <span>Valor</span>
                <span>% da receita</span>
                <span>vs. média 3 meses</span>
              </div>
              {rankingTop10.map(c => {
                const badge = badgeVariacao(c)
                return (
                  <div key={c.nome} className="ind-ranking-item">
                    <div className="ind-ranking-linha" onClick={() => toggleCategoria(c.nome)}>
                      <span className="ind-ranking-nome">{c.nome}</span>
                      <span className="ind-ranking-valor-cel">
                        <span className="ind-ranking-barra" style={{ width: `${maiorValorRanking > 0 ? (c.total / maiorValorRanking) * 100 : 0}%` }} />
                        <span className="ind-ranking-valor">{fmtBRL(c.total)}</span>
                      </span>
                      <span className="ind-ranking-pct">{fmtPct(c.percentualReceita)}</span>
                      <span className={`ind-badge ${badge.classe}`}>{badge.texto}</span>
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
                )
              })}
              {rankingOutras.length > 0 && (
                <div className="ind-ranking-item">
                  <div className="ind-ranking-linha" onClick={() => setOutrasAberta(a => !a)}>
                    <span className="ind-ranking-nome">{outrasAberta ? '▾' : '▸'} Outras ({rankingOutras.length})</span>
                    <span className="ind-ranking-valor-cel">
                      <span className="ind-ranking-barra" style={{ width: `${maiorValorRanking > 0 ? (outrasTotal / maiorValorRanking) * 100 : 0}%` }} />
                      <span className="ind-ranking-valor">{fmtBRL(outrasTotal)}</span>
                    </span>
                    <span className="ind-ranking-pct">{fmtPct(outrasPercentualReceita)}</span>
                    <span className="ind-badge" />
                  </div>
                  {outrasAberta && rankingOutras.map(c => {
                    const badge = badgeVariacao(c)
                    return (
                      <div key={c.nome} className="ind-ranking-linha ind-ranking-linha-outra" onClick={() => toggleCategoria(c.nome)}>
                        <span className="ind-ranking-nome">{c.nome}</span>
                        <span className="ind-ranking-valor-cel">
                          <span className="ind-ranking-barra" style={{ width: `${maiorValorRanking > 0 ? (c.total / maiorValorRanking) * 100 : 0}%` }} />
                          <span className="ind-ranking-valor">{fmtBRL(c.total)}</span>
                        </span>
                        <span className="ind-ranking-pct">{fmtPct(c.percentualReceita)}</span>
                        <span className={`ind-badge ${badge.classe}`}>{badge.texto}</span>
                      </div>
                    )
                  })}
                  {outrasAberta && rankingOutras.map(c => categoriaAberta === c.nome && (
                    <div key={`${c.nome}-lancamentos`} className="ind-lancamentos">
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
                  ))}
                </div>
              )}
            </div>
          </>
        )}
      </section>

      {/* BLOCO 3 — Crescendo ou estagnado */}
      <section className="mv-answer-card">
        <h2 className="mv-answer-pergunta">Estou crescendo ou estagnado?</h2>

        {!historicoSuficiente ? (
          <p className="ind-vazio">
            Ainda não há histórico suficiente (menos de 3 meses de movimentação) para avaliar se o negócio está
            crescendo. Continue lançando seus dados e essa análise vai ficar mais precisa.
          </p>
        ) : (
          <>
            {indicadores.variacaoReceitaMesAnterior !== null && (
              <div style={{
                fontSize: 22, fontWeight: 700, marginBottom: 12,
                color: indicadores.variacaoReceitaMesAnterior >= 0 ? 'var(--success)' : 'var(--danger)',
              }}>
                {indicadores.variacaoReceitaMesAnterior >= 0 ? '▲' : '▼'} {fmtPctBase(Math.abs(indicadores.variacaoReceitaMesAnterior))}
                <span style={{ fontSize: 13, fontWeight: 500, color: 'var(--tx2)' }}> vs. mês passado</span>
              </div>
            )}
            <div className="mv-chart-card" style={{ height: 220 }}>
              <ResponsiveContainer width="100%" height="100%">
                <AreaChart data={dadosGraficoReceita}>
                  <defs>
                    <linearGradient id="gradReceitaInd" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="5%" stopColor="var(--mv-blue)" stopOpacity={0.28} />
                      <stop offset="95%" stopColor="var(--mv-blue)" stopOpacity={0.02} />
                    </linearGradient>
                  </defs>
                  <CartesianGrid stroke="var(--tx2)" strokeOpacity={0.12} vertical={false} />
                  <XAxis dataKey="mes" tick={{ fontSize: 11, fill: 'var(--tx2)' }} axisLine={{ stroke: 'var(--tx2)', strokeOpacity: 0.3 }} tickLine={false} />
                  <YAxis tick={{ fontSize: 11, fill: 'var(--tx3)' }} tickFormatter={v => fmtBRLCompacto(v)} width={60} axisLine={false} tickLine={false} />
                  <Tooltip
                    formatter={(v, name) => [typeof v === 'number' ? fmtBRL(v) : String(v ?? ''), name === 'receita' ? 'Receita' : 'Tendência']}
                    contentStyle={{ background: 'var(--bg-card)', border: '1px solid var(--bd)', borderRadius: 10 }}
                    labelStyle={{ color: 'var(--tx2)' }}
                  />
                  <Area type="monotone" dataKey="receita" name="receita" stroke="var(--mv-blue)" strokeWidth={2} fill="url(#gradReceitaInd)" dot={{ r: 3 }} isAnimationActive={!semAnimacao} />
                  <Line type="linear" dataKey="tendencia" name="tendencia" stroke="var(--tx3)" strokeWidth={1.5} strokeDasharray="4 4" dot={false} isAnimationActive={!semAnimacao} />
                </AreaChart>
              </ResponsiveContainer>
            </div>
            <MvInsight text={leituraTendencia} tone="neutral" />
          </>
        )}
      </section>

      {/* BLOCO 4 — Ponto de Equilíbrio */}
      <section className="mv-answer-card">
        <h2 className="mv-answer-pergunta">Quanto preciso vender pra não ter prejuízo?</h2>
        {!pontoEquilibrio.disponivel ? (
          <p className="ind-vazio">{pontoEquilibrio.motivoIndisponivel}</p>
        ) : (
          <>
            <div style={{ textAlign: 'center' }}>
              <span style={{ fontSize: 32, fontWeight: 800, color: 'var(--tx1)' }}>{fmtBRL(pontoEquilibrio.valorMensal ?? 0)}</span>
              <span style={{ fontSize: 14, fontWeight: 500, color: 'var(--tx2)' }}> /mês</span>
            </div>
            <div className="mv-progress-track" style={{ marginTop: 18 }}>
              <div className="mv-progress-fill" style={{
                width: `${Math.min(100, (pontoEquilibrio.receitaAtual / peMax) * 100)}%`,
                background: peAcimaDoEquilibrio ? 'var(--success)' : 'var(--danger)',
              }} />
              <div className="mv-progress-marker" style={{ left: `${((pontoEquilibrio.valorMensal ?? 0) / peMax) * 100}%` }} />
              <div className="mv-progress-marker-label" style={{ left: `${((pontoEquilibrio.valorMensal ?? 0) / peMax) * 100}%` }}>Equilíbrio</div>
            </div>
            <p style={{ fontSize: 12.5, color: 'var(--tx2)', margin: '4px 0 0' }}>
              Receita atual: <strong style={{ color: 'var(--tx1)' }}>{fmtBRL(pontoEquilibrio.receitaAtual)}</strong>
              {pontoEquilibrio.valorPorDiaUtil !== null && <> · {fmtBRL(pontoEquilibrio.valorPorDiaUtil)} por dia útil ({pontoEquilibrio.diasUteisNoMes} dias úteis neste mês)</>}
            </p>
            <MvInsight text={leituraPontoEquilibrio(pontoEquilibrio)} tone={peAcimaDoEquilibrio ? 'good' : 'bad'} />
          </>
        )}
      </section>

      {/* BLOCO 5 — Fôlego de Caixa */}
      <section className="mv-answer-card">
        <h2 className="mv-answer-pergunta">Se parar de entrar dinheiro, quanto tempo eu duro?</h2>
        {!folegoCaixa.disponivel ? (
          <p className="ind-vazio">{folegoCaixa.motivoIndisponivel}</p>
        ) : (
          <>
            <div style={{ textAlign: 'center' }}>
              <span style={{ fontSize: 32, fontWeight: 800, color: corFolego }}>
                {fmtNumDecimal(folegoCaixa.meses ?? 0)} {folegoCaixa.meses === 1 ? 'mês' : 'meses'}
              </span>
            </div>
            <div className="mv-progress-track" style={{ marginTop: 18 }}>
              <div className="mv-progress-fill" style={{ width: `${Math.min(100, ((folegoCaixa.meses ?? 0) / 12) * 100)}%`, background: corFolego }} />
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 11, color: 'var(--tx3)', marginTop: 2 }}>
              <span>0</span><span>12 meses</span>
            </div>
            <p style={{ fontSize: 12.5, color: 'var(--tx2)', margin: '10px 0 0' }}>
              Saldo disponível: <strong style={{ color: 'var(--tx1)' }}>{fmtBRL(folegoCaixa.saldoDisponivel)}</strong> · Custo fixo médio: <strong style={{ color: 'var(--tx1)' }}>{fmtBRL(folegoCaixa.custoFixoMedioMensal ?? 0)}</strong>/mês
            </p>
            <MvInsight text={leituraFolegoCaixa(folegoCaixa)} tone={folegoCaixa.faixa === 'critico' ? 'bad' : folegoCaixa.faixa === 'atencao' ? 'warn' : 'good'} />
          </>
        )}
      </section>

      {/* BLOCO 6 — Custo Fixo ÷ Receita */}
      <section className="mv-answer-card">
        <h2 className="mv-answer-pergunta">Posso assumir um custo fixo novo?</h2>
        {!custoFixoHistoricoSuficiente ? (
          <p className="ind-vazio">
            Ainda não há histórico suficiente (menos de 3 meses com movimento) para avaliar a tendência do custo fixo.
          </p>
        ) : (
          <>
            <div className="mv-gauge-wrap">
              <MvGauge
                value={mesAtualCustoFixo?.percentual ?? 0}
                min={0} max={100}
                zonas={[{ ate: 40, cor: 'var(--success)' }, { ate: 60, cor: 'var(--warning)' }, { ate: 100, cor: 'var(--danger)' }]}
                marcador={60}
                valorFormatado={fmtPct(mesAtualCustoFixo?.percentual ?? null)}
                legenda="custo fixo da receita"
              />
            </div>
            <div className="mv-chart-card" style={{ height: 160 }}>
              <ResponsiveContainer width="100%" height="100%">
                <AreaChart data={custoFixoSerieAtiva.map(m => ({ mes: mesLabel(m.mes), percentual: m.percentual }))}>
                  <CartesianGrid stroke="var(--tx2)" strokeOpacity={0.12} vertical={false} />
                  <XAxis dataKey="mes" tick={{ fontSize: 11, fill: 'var(--tx2)' }} axisLine={{ stroke: 'var(--tx2)', strokeOpacity: 0.3 }} tickLine={false} />
                  <YAxis tick={{ fontSize: 11, fill: 'var(--tx3)' }} tickFormatter={v => `${v}%`} width={40} axisLine={false} tickLine={false} />
                  <Tooltip
                    formatter={v => typeof v === 'number' ? fmtPctBase(v) : String(v)}
                    contentStyle={{ background: 'var(--bg-card)', border: '1px solid var(--bd)', borderRadius: 10 }}
                    labelStyle={{ color: 'var(--tx2)' }}
                  />
                  <Area type="monotone" dataKey="percentual" name="Custo fixo / receita" stroke="var(--warning)" strokeWidth={2} fill="var(--warning)" fillOpacity={0.12} dot={{ r: 3, fill: 'var(--warning)' }} isAnimationActive={!semAnimacao} />
                </AreaChart>
              </ResponsiveContainer>
            </div>
            <MvInsight text={leituraCustoFixo} tone={toneLeituraCustoFixo} />
          </>
        )}
      </section>

      {/* BLOCO 7 — Prazo Médio de Recebimento */}
      <section className="mv-answer-card">
        <h2 className="mv-answer-pergunta">Meu problema é faturamento ou é recebimento?</h2>
        {!prazoRecebimento.disponivel ? (
          <p className="ind-vazio">{prazoRecebimento.motivoIndisponivel}</p>
        ) : (
          <>
            <div className="mv-gauge-wrap">
              <MvGauge
                value={prazoRecebimento.mediaDias ?? 0}
                min={-10} max={30}
                zonas={[{ ate: 0, cor: 'var(--success)' }, { ate: 10, cor: 'var(--warning)' }, { ate: 30, cor: 'var(--danger)' }]}
                valorFormatado={`${(prazoRecebimento.mediaDias ?? 0) > 0 ? '+' : ''}${fmtNumDecimal(prazoRecebimento.mediaDias ?? 0)}d`}
                legenda="dias de atraso médio"
              />
            </div>
            <p style={{ textAlign: 'center', fontSize: 12.5, color: 'var(--tx2)', margin: '0 0 12px' }}>
              Com base em {prazoRecebimento.quantidadeAmostras} conta(s) a receber já baixadas
            </p>
            <MvInsight text={leituraPrazoRecebimento(prazoRecebimento)} tone={(prazoRecebimento.mediaDias ?? 0) > 10 ? 'bad' : (prazoRecebimento.mediaDias ?? 0) > 0 ? 'warn' : 'good'} />
          </>
        )}
      </section>
    </MvPage>
  )
}
