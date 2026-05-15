import { fmtDate } from '../../utils/format'

interface DayNavProps {
  date: string
  onPrev: () => void
  onNext: () => void
}
export default function DayNav({ date, onPrev, onNext }: DayNavProps) {
  const dayNames = ['Dom','Seg','Ter','Qua','Qui','Sex','Sáb']
  const dayName = dayNames[new Date(`${date}T12:00:00`).getDay()]

  return (
    <div className="day-nav">
      <button onClick={onPrev}>←</button>
      <div>
        <div className="day-label">{fmtDate(date)}</div>
        <div className="day-sub">{dayName}</div>
      </div>
      <button onClick={onNext}>→</button>
    </div>
  )
}
