import { BarChart, Bar, CartesianGrid, XAxis, YAxis, Tooltip, ResponsiveContainer, Cell } from 'recharts'
import { MvSectionLabel, MvInsight, prefereMovimentoReduzido } from '../../../components/mv'
import { fmtBRL, fmtBRLCompacto, fmtPct } from '../../../utils/format'
import type { ParaOndeFoiMeuDinheiro } from '../../../api/metricas'
import '../../../components/mv/mv.css'

interface Props {
  dados: ParaOndeFoiMeuDinheiro
}

interface Barra {
  nome: string
  base: number
  valor: number
  cor: string
  categorias?: { nome: string; total: number }[]
}

/** Redesign visual (Money Vision): "Para onde foi meu dinheiro" como waterfall horizontal —
 * Entrou → Gastou (com as top 3 categorias no tooltip) → Guardou/Resgatou → Ficou na conta. Cada
 * barra "flutua" sobre uma base invisível pra representar o acumulado corretamente. */
export default function DinheiroFluxoCard({ dados }: Props) {
  const { entrou, gastou, guardou, topCategoriasGasto } = dados

  const acc1 = entrou
  const acc2 = acc1 - gastou
  const acc3 = acc2 - guardou // === ficou

  const barras: Barra[] = [
    { nome: 'Entrou', base: 0, valor: acc1, cor: 'var(--mv-blue)' },
    { nome: 'Gastou', base: Math.min(acc1, acc2), valor: Math.abs(gastou), cor: 'var(--danger)', categorias: topCategoriasGasto },
    {
      nome: guardou >= 0 ? 'Guardou' : 'Resgatou',
      base: Math.min(acc2, acc3), valor: Math.abs(guardou),
      cor: guardou >= 0 ? 'var(--success)' : 'var(--warning)',
    },
    { nome: 'Ficou na conta', base: 0, valor: acc3, cor: 'var(--mv-blue)' },
  ]

  function tooltip(props: { active?: boolean; payload?: readonly { payload?: Barra }[] }) {
    const { active, payload } = props
    if (!active || !payload || payload.length === 0) return null
    const item = payload[0]?.payload
    if (!item) return null
    return (
      <div className="mv-chart-tooltip">
        <div className="mv-chart-tooltip-label">{item.nome}</div>
        <div className="mv-chart-tooltip-row"><span>Valor</span><strong>{fmtBRL(item.valor)}</strong></div>
        {item.categorias && item.categorias.length > 0 && (
          <>
            <div className="mv-chart-tooltip-label" style={{ marginTop: 6 }}>Maiores categorias</div>
            {item.categorias.map(c => (
              <div key={c.nome} className="mv-chart-tooltip-row">
                <span>{c.nome}</span><strong>{fmtBRL(c.total)}</strong>
              </div>
            ))}
          </>
        )}
      </div>
    )
  }

  const semAnimacao = prefereMovimentoReduzido()

  const frase = guardou > 0
    ? <>Você guardou <strong>{fmtBRL(guardou)}</strong> este mês{dados.taxaPoupancaPercentual !== null && <> — taxa de poupança de <strong>{fmtPct(dados.taxaPoupancaPercentual)}</strong></>}.</>
    : guardou < 0
      ? <>Você resgatou <strong>{fmtBRL(-guardou)}</strong> da reserva este mês.</>
      : 'Nenhuma aplicação ou resgate em investimento este mês.'

  return (
    <div>
      <MvSectionLabel>PARA ONDE FOI MEU DINHEIRO</MvSectionLabel>
      <div className="mv-chart-card" style={{ height: 220 }}>
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={barras} margin={{ top: 8, right: 8, bottom: 0, left: 0 }}>
            <CartesianGrid stroke="var(--tx2)" strokeOpacity={0.12} vertical={false} />
            <XAxis dataKey="nome" tick={{ fontSize: 12, fill: 'var(--tx2)' }} axisLine={{ stroke: 'var(--tx2)', strokeOpacity: 0.3 }} tickLine={false} />
            <YAxis tick={{ fontSize: 11, fill: 'var(--tx3)' }} tickFormatter={v => fmtBRLCompacto(v)} width={64} axisLine={false} tickLine={false} />
            <Tooltip content={tooltip} cursor={{ fill: 'var(--bg-hover)' }} />
            <Bar dataKey="base" stackId="fluxo" fill="transparent" isAnimationActive={false} />
            <Bar dataKey="valor" stackId="fluxo" radius={[6, 6, 6, 6]} isAnimationActive={!semAnimacao}>
              {barras.map((b, i) => <Cell key={i} fill={b.cor} />)}
            </Bar>
          </BarChart>
        </ResponsiveContainer>
      </div>
      <MvInsight icon="💬" text={frase} tone={guardou >= 0 ? 'good' : 'warn'} />
    </div>
  )
}
