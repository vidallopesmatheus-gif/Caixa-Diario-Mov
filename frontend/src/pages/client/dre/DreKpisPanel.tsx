import { useMemo, useState } from 'react'
import { fmtBRL, fmtPct, fmtDate } from '../../../utils/format'
import { ehOperacional } from '../../../utils/lancamentos'
import { leituraMargemDre } from '../../../utils/leituras'
import { calcularRankingFavorecidos, type RankingFavorecido } from '../../../utils/favorecidos'
import { calcularConcentracaoPorDia } from '../../../utils/concentracao'
import Modal from '../../../components/shared/Modal'
import type { Dre } from '../../../api/metricas'
import type { Registro } from '../../../types'
import type { TipoPeriodo } from '../ClientDrePage'
import '../ClientDre.css'
import './DreKpisPanel.css'

interface Props {
  dre: Dre
  tipoPeriodo: TipoPeriodo
  registros: Registro[]
  de: string
  ate: string
  contaFiltro: string
}

function leituraMargem(percentual: number): { texto: string; cor: string } {
  if (percentual >= 15) return { texto: 'Saudável', cor: 'var(--success)' }
  if (percentual >= 0) return { texto: 'Apertada', cor: 'var(--warning)' }
  return { texto: 'Negativa', cor: 'var(--danger)' }
}

function Sparkline({ pontos, corLinha }: { pontos: { mes: string; valor: number; temDados: boolean }[]; corLinha: string }) {
  const largura = 220
  const altura = 48
  const comDados = pontos.filter(p => p.temDados)
  if (comDados.length === 0) return null

  const valores = pontos.map(p => p.valor)
  const min = Math.min(0, ...valores)
  const max = Math.max(0, ...valores)
  const amplitude = max - min || 1
  const passo = largura / (pontos.length - 1 || 1)

  const coords = pontos.map((p, i) => {
    const x = i * passo
    const y = altura - ((p.valor - min) / amplitude) * altura
    return { x, y, temDados: p.temDados }
  })

  const linha = coords.map(c => `${c.x},${c.y}`).join(' ')
  const yZero = altura - ((0 - min) / amplitude) * altura

  return (
    <svg width={largura} height={altura + 4} className="dre-kpi-sparkline">
      <line x1={0} y1={yZero} x2={largura} y2={yZero} className="dre-kpi-sparkline-eixo" />
      <polyline points={linha} fill="none" stroke={corLinha} strokeWidth={2} />
      {coords.map((c, i) => (
        <circle key={pontos[i].mes} cx={c.x} cy={c.y} r={c.temDados ? 2.5 : 0} fill={corLinha} />
      ))}
    </svg>
  )
}

function CardFavorecidos({
  titulo, subtitulo, ranking,
}: { titulo: string; subtitulo: string; ranking: RankingFavorecido[] }) {
  const [aberto, setAberto] = useState<RankingFavorecido | null>(null)

  return (
    <div className="dre-kpi-card">
      <div className="dre-kpi-titulo">{titulo}</div>
      <div className="dre-kpi-subtitulo">{subtitulo}</div>
      {ranking.length === 0 ? (
        <p className="dre-kpi-vazio">Nenhum lançamento no período.</p>
      ) : (
        <div className="dre-kpi-ranking">
          {ranking.map(r => (
            <button key={r.rotulo} type="button" className="dre-kpi-ranking-item" onClick={() => setAberto(r)}>
              <span className="dre-kpi-ranking-rotulo">{r.rotulo}</span>
              <span className="dre-kpi-ranking-meta">{r.ocorrencias}x</span>
              <span className="dre-kpi-ranking-valor">{fmtBRL(r.total)}</span>
            </button>
          ))}
        </div>
      )}

      <Modal open={!!aberto} title={aberto?.rotulo ?? ''} onClose={() => setAberto(null)}>
        {aberto && (
          <div className="dre-drill-lista">
            {aberto.lancamentos
              .slice()
              .sort((a, b) => a.data.localeCompare(b.data))
              .map((l, i) => (
                <div key={i} className="dre-drill-item">
                  <span className="dre-drill-data">{fmtDate(l.data)}</span>
                  <span className="dre-drill-desc">{l.descricao}</span>
                  <span className="dre-drill-valor">{fmtBRL(l.valor)}</span>
                </div>
              ))}
          </div>
        )}
      </Modal>
    </div>
  )
}

export default function DreKpisPanel({ dre, tipoPeriodo, registros, de, ate, contaFiltro }: Props) {
  const registrosDoPeriodo = useMemo(
    () => registros.filter(r => r.data >= de && r.data <= ate && (!contaFiltro || r.contaBancariaId === contaFiltro)),
    [registros, de, ate, contaFiltro]
  )

  const rankingFavorecidos = useMemo(() => {
    const saidas = registrosDoPeriodo
      .flatMap(r => r.saidas.filter(ehOperacional).map(s => ({ data: r.data, descricao: s.descricao, valor: s.valor })))
    return calcularRankingFavorecidos(saidas, 'Saida')
  }, [registrosDoPeriodo])

  const rankingPagadores = useMemo(() => {
    const entradas = registrosDoPeriodo
      .flatMap(r => r.entradas.filter(ehOperacional).map(e => ({ data: r.data, descricao: e.descricao, valor: e.valor })))
    return calcularRankingFavorecidos(entradas, 'Entrada')
  }, [registrosDoPeriodo])

  // Mês (ou período menor) usa recorte por dia; trimestre/ano usam recorte por mês — muda sozinho
  // com o filtro de período da tela, sem o usuário configurar nada.
  const granularidadeConcentracao = tipoPeriodo === 'mes' ? 'dia' : 'mes'
  const concentracao = useMemo(
    () => calcularConcentracaoPorDia(registrosDoPeriodo, granularidadeConcentracao),
    [registrosDoPeriodo, granularidadeConcentracao]
  )

  const temReceita = dre.receitaBruta > 0 && dre.resultadoLiquidoPercentual !== null
  const leitura = temReceita ? leituraMargem(dre.resultadoLiquidoPercentual!) : null

  const pe = dre.pontoEquilibrio

  const evolucao = dre.evolucaoResultadoLiquido ?? []
  const mesesComDado = evolucao.filter(m => m.temDados).length
  const pontosSparkline = evolucao.map(m => ({ mes: m.mes, valor: m.resultadoLiquido, temDados: m.temDados }))
  const ultimoResultado = evolucao[evolucao.length - 1]?.resultadoLiquido ?? 0

  const semConcentracao = granularidadeConcentracao === 'dia'
    ? !concentracao.entradaDiaSemana && !concentracao.saidaDiaSemana
    : !concentracao.entradaMes && !concentracao.saidaMes

  return (
    <div className="dre-kpis-painel">
      <div className="dre-kpi-bloco">
        <h3 className="dre-kpi-bloco-titulo">Leitura do resultado</h3>

        {/* 1 — Termômetro do Resultado */}
        <div className="dre-kpi-card">
          <div className="dre-kpi-titulo">Termômetro do Resultado</div>
          {!temReceita ? (
            <p className="dre-kpi-vazio">Sem receita no período para calcular a margem.</p>
          ) : (
            <>
              <div className="dre-kpi-termometro-valor" style={{ color: leitura!.cor }}>
                {fmtPct(dre.resultadoLiquidoPercentual!)}
              </div>
              <p className="dre-kpi-subtitulo">{leituraMargemDre(dre.resultadoLiquidoPercentual)}</p>
              <span className="dre-kpi-faixa" style={{ color: leitura!.cor, borderColor: leitura!.cor }}>
                {leitura!.texto}
              </span>
            </>
          )}
        </div>

        {/* 2 — Ponto de Equilíbrio */}
        <div className="dre-kpi-card">
          <div className="dre-kpi-titulo">Ponto de Equilíbrio</div>
          {!pe || !pe.disponivel ? (
            <p className="dre-kpi-vazio">{pe?.motivoIndisponivel ?? 'Sem dados suficientes para calcular.'}</p>
          ) : (
            <>
              <div className="dre-kpi-termometro-valor">{fmtBRL(pe.valorMensal ?? 0)}</div>
              <p className="dre-kpi-subtitulo">Faturamento necessário no período para zerar o resultado.</p>
              {pe.distancia !== null && (
                <p className="dre-kpi-subtitulo">
                  {pe.distancia >= 0 ? (
                    <>Faturamento atual está <strong style={{ color: 'var(--success)' }}>{fmtBRL(pe.distancia)}</strong> ({fmtPct(pe.distanciaPercentual ?? 0)}) acima do ponto de equilíbrio.</>
                  ) : (
                    <>Faturamento atual está <strong style={{ color: 'var(--danger)' }}>{fmtBRL(Math.abs(pe.distancia))}</strong> ({fmtPct(Math.abs(pe.distanciaPercentual ?? 0))}) abaixo do ponto de equilíbrio.</>
                  )}
                </p>
              )}
            </>
          )}
        </div>

        {/* 3 — Evolução do Resultado */}
        <div className="dre-kpi-card">
          <div className="dre-kpi-titulo">Evolução do Resultado</div>
          {mesesComDado < 3 ? (
            <p className="dre-kpi-vazio">Ainda não há histórico suficiente (mínimo 3 meses) para mostrar a evolução.</p>
          ) : (
            <>
              <Sparkline pontos={pontosSparkline} corLinha={ultimoResultado >= 0 ? 'var(--success)' : 'var(--danger)'} />
              <p className="dre-kpi-subtitulo">Resultado líquido — últimos {evolucao.length} meses.</p>
            </>
          )}
        </div>
      </div>

      <div className="dre-kpi-bloco">
        <h3 className="dre-kpi-bloco-titulo">Análise de padrão</h3>

        {/* 4 — Maiores Favorecidos */}
        <CardFavorecidos titulo="Maiores Favorecidos" subtitulo="Para quem mais saiu dinheiro no período" ranking={rankingFavorecidos} />

        {/* 5 — Maiores Pagadores */}
        <CardFavorecidos titulo="Maiores Pagadores" subtitulo="De quem mais entrou dinheiro no período" ranking={rankingPagadores} />

        {/* 6 — Concentração por Dia */}
        <div className="dre-kpi-card">
          <div className="dre-kpi-titulo">Concentração {granularidadeConcentracao === 'dia' ? 'por Dia' : 'por Mês'}</div>
          {semConcentracao ? (
            <p className="dre-kpi-vazio">Nenhum lançamento no período.</p>
          ) : granularidadeConcentracao === 'dia' ? (
            <div className="dre-kpi-concentracao">
              <div className="dre-kpi-concentracao-linha">
                <span className="dre-kpi-concentracao-label val-green">Entrada concentra:</span>
                <span>
                  {concentracao.entradaDiaSemana ? `${concentracao.entradaDiaSemana.rotulo} (${fmtBRL(concentracao.entradaDiaSemana.total)})` : '—'}
                  {concentracao.entradaDiaMes ? ` · dia ${concentracao.entradaDiaMes.rotulo} do mês (${fmtBRL(concentracao.entradaDiaMes.total)})` : ''}
                </span>
              </div>
              <div className="dre-kpi-concentracao-linha">
                <span className="dre-kpi-concentracao-label val-red">Saída concentra:</span>
                <span>
                  {concentracao.saidaDiaSemana ? `${concentracao.saidaDiaSemana.rotulo} (${fmtBRL(concentracao.saidaDiaSemana.total)})` : '—'}
                  {concentracao.saidaDiaMes ? ` · dia ${concentracao.saidaDiaMes.rotulo} do mês (${fmtBRL(concentracao.saidaDiaMes.total)})` : ''}
                </span>
              </div>
            </div>
          ) : (
            <div className="dre-kpi-concentracao">
              <div className="dre-kpi-concentracao-linha">
                <span className="dre-kpi-concentracao-label val-green">Maior entrada:</span>
                <span>{concentracao.entradaMes ? `${concentracao.entradaMes.rotulo} (${fmtBRL(concentracao.entradaMes.total)})` : '—'}</span>
              </div>
              <div className="dre-kpi-concentracao-linha">
                <span className="dre-kpi-concentracao-label val-red">Maior saída:</span>
                <span>{concentracao.saidaMes ? `${concentracao.saidaMes.rotulo} (${fmtBRL(concentracao.saidaMes.total)})` : '—'}</span>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
