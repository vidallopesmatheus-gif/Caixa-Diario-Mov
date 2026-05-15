interface StatCardProps {
  label: string
  value: string
  className?: string
}
export default function StatCard({ label, value, className = '' }: StatCardProps) {
  return (
    <div className="stat-card">
      <div className="lbl">{label}</div>
      <div className={`val ${className}`}>{value}</div>
    </div>
  )
}
