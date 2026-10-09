interface StatCardProps {
  label: string
  value: string
  className?: string
  sub?: string
  /** Tooltip nativo (ex.: explicar a fórmula por trás do número) — aparece ao passar o mouse. */
  title?: string
}
export default function StatCard({ label, value, className = '', sub, title }: StatCardProps) {
  return (
    <div className="stat-card" title={title}>
      <div className="lbl">{label}</div>
      <div className={`val ${className}`}>{value}</div>
      {sub && <div className="stat-sub" style={{ fontSize: 11, color: 'var(--tx3)', marginTop: 4 }}>{sub}</div>}
    </div>
  )
}
