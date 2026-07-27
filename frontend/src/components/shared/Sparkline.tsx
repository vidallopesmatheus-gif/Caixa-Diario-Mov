import { LineChart, Line, ResponsiveContainer } from 'recharts'

interface Props {
  valores: number[]
  cor?: string
}

/**
 * Micro-gráfico de contexto: sem eixos, grid, legenda ou tooltip.
 * Não é protagonista — só sugere a forma da tendência recente ao lado do número principal.
 */
export default function Sparkline({ valores, cor = 'var(--tx3)' }: Props) {
  if (valores.length < 2) return null
  const dados = valores.map((v, i) => ({ i, v }))

  return (
    <div style={{ width: '100%', height: 28 }}>
      <ResponsiveContainer width="100%" height="100%">
        <LineChart data={dados} margin={{ top: 2, right: 2, bottom: 2, left: 2 }}>
          <Line type="monotone" dataKey="v" stroke={cor} strokeWidth={1.5} dot={false} isAnimationActive={false} />
        </LineChart>
      </ResponsiveContainer>
    </div>
  )
}
