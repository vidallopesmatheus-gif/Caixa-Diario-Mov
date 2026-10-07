import { fmtDate } from '../../utils/format'

interface DayNavProps {
  date: string
  onPrev: () => void
  onNext: () => void
  // Data local (YYYY-MM-DD) além da qual não se pode avançar — desabilita "→" e trava o seletor.
  // Sem isso, o usuário conseguia navegar/lançar em dias que ainda não aconteceram.
  max?: string
  // Seletor de data direto no cabeçalho — evita ter que clicar em "→" várias vezes pra voltar/ir
  // pra um dia distante.
  onPick?: (date: string) => void
}
export default function DayNav({ date, onPrev, onNext, max, onPick }: DayNavProps) {
  const dayNames = ['Dom','Seg','Ter','Qua','Qui','Sex','Sáb']
  const dayName = dayNames[new Date(`${date}T12:00:00`).getDay()]
  const noFuturo = !!max && date >= max

  return (
    <div className="day-nav">
      <button onClick={onPrev}>←</button>
      <div style={{ textAlign: 'center' }}>
        <div className="day-label">{fmtDate(date)}</div>
        <div className="day-sub">{dayName}</div>
        {onPick && (
          <input
            type="date"
            aria-label="Escolher data"
            value={date}
            max={max}
            onChange={e => e.target.value && onPick(e.target.value)}
            style={{ marginTop: 4, fontSize: 11, padding: '2px 4px', borderRadius: 6, border: '1px solid var(--bd)', background: 'var(--bg-input)', color: 'var(--tx1)' }}
          />
        )}
      </div>
      <button onClick={onNext} disabled={noFuturo} title={noFuturo ? 'Não é possível avançar para o futuro' : undefined}>→</button>
    </div>
  )
}
