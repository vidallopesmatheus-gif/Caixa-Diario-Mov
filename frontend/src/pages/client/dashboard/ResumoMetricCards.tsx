import { useState, useEffect } from 'react'
import { obterEvolucao } from '../../../api/metricas'
import type { Dre, EvolucaoMensal } from '../../../api/metricas'
import type { Projecao } from '../../../api/projecao'
import type { JanelaPeriodo } from '../../../utils/periodo'
import MetricCard from './MetricCard'

interface Props {
  clienteId: string
  janela: JanelaPeriodo
  dreAtual: Dre | null
  dreAnterior: Dre | null
  saldoAtual: number | null
  // Vem de cima (ClientDashboardPage) — mesma chamada a /api/projecao usada no gráfico de
  // fluxo de caixa logo abaixo, pra não duplicar a busca (era uma segunda fonte pra "projeção
  // de saldo", com a lógica de dedup de recorrência já materializada implementada só numa delas).
  projecao: Projecao | null
}

function variacaoPct(atual: number, base: number): number | null {
  if (base === 0) return null
  return ((atual - base) / Math.abs(base)) * 100
}

export default function ResumoMetricCards({ clienteId, janela, dreAtual, dreAnterior, saldoAtual, projecao }: Props) {
  const [evolucao, setEvolucao] = useState<EvolucaoMensal[]>([])

  useEffect(() => {
    if (!clienteId) return
    obterEvolucao(clienteId, 8).then(setEvolucao).catch(() => setEvolucao([]))
  }, [clienteId])

  const entradas = dreAtual?.receitaBruta ?? null
  const saidas = dreAtual?.totalDespesas ?? null
  const entradasVar = dreAtual && dreAnterior ? variacaoPct(dreAtual.receitaBruta, dreAnterior.receitaBruta) : null
  const saidasVar = dreAtual && dreAnterior ? variacaoPct(dreAtual.totalDespesas, dreAnterior.totalDespesas) : null

  const saldoProjetado = projecao ? (projecao.dias.at(-1)?.saldoFim ?? projecao.saldoAtual) : null
  const saldoProjetadoVar = saldoProjetado !== null && saldoAtual !== null ? variacaoPct(saldoProjetado, saldoAtual) : null

  // Saldo no início do período = saldo atual menos o resultado (entradas - saídas) já ocorrido no período.
  const saldoInicioPeriodo = saldoAtual !== null && entradas !== null && saidas !== null
    ? saldoAtual - (entradas - saidas)
    : null
  const saldoAtualVar = saldoAtual !== null && saldoInicioPeriodo !== null
    ? variacaoPct(saldoAtual, saldoInicioPeriodo)
    : null

  const serieSaldo = evolucao.map(e => e.saldo)
  const serieEntradas = evolucao.map(e => e.receita)
  const serieSaidas = evolucao.map(e => e.custos)
  const serieProjecao = projecao ? projecao.dias.map(d => d.saldoFim) : []

  return (
    <div className="resumo-cards-grid">
      <MetricCard
        label="Saldo Atual"
        valor={saldoAtual}
        variacaoPct={saldoAtualVar}
        comparativoLabel="vs. início do período"
        serie={serieSaldo}
      />
      <MetricCard
        label={`Entradas (${janela.label})`}
        valor={entradas}
        variacaoPct={entradasVar}
        comparativoLabel="vs. período anterior"
        serie={serieEntradas}
      />
      <MetricCard
        label={`Saídas (${janela.label})`}
        valor={saidas}
        variacaoPct={saidasVar}
        comparativoLabel="vs. período anterior"
        serie={serieSaidas}
        corPositivo={false}
      />
      <MetricCard
        label="Saldo Projetado (30 dias)"
        valor={saldoProjetado}
        variacaoPct={saldoProjetadoVar}
        comparativoLabel="vs. saldo atual"
        serie={serieProjecao}
      />
    </div>
  )
}
