interface StatCardProps {
  label: string
  value: string
  className?: string
  sub?: string
}
export default function StatCard({ label, value, className = '', sub }: StatCardProps) {
  return (
    <div className="stat-card">
      <div className="lbl">{label}</div>
      <div className={`val ${className}`}>{value}</div>
      {sub && <div className="stat-sub" style={{ fontSize: 11, color: 'var(--tx3)', marginTop: 4 }}>{sub}</div>}
    </div>
  )
}
