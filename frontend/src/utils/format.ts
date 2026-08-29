export function fmtBRL(value: number): string {
  return value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
}

/** Percentual no padrão pt-BR (vírgula decimal) — "12,3%" em vez de "12.3%". */
export function fmtPct(value: number, casas = 1): string {
  return `${value.toLocaleString('pt-BR', { minimumFractionDigits: casas, maximumFractionDigits: casas })}%`
}

export function fmtDate(iso: string): string {
  const [y, m, d] = iso.split('-')
  return `${d}/${m}/${y}`
}

export function todayISO(): string {
  return new Date().toISOString().slice(0, 10)
}

export function addDays(iso: string, days: number): string {
  const d = new Date(iso + 'T12:00:00')
  d.setDate(d.getDate() + days)
  return d.toISOString().slice(0, 10)
}

export function monthLabel(iso: string): string {
  const [y, m] = iso.split('-')
  const months = ['Jan','Fev','Mar','Abr','Mai','Jun','Jul','Ago','Set','Out','Nov','Dez']
  return `${months[parseInt(m) - 1]}/${y}`
}

/** Desloca um mês no formato "YYYY-MM" por `delta` meses (aceita negativos). */
export function addMonths(mesIso: string, delta: number): string {
  const [y, m] = mesIso.split('-').map(Number)
  const total = y * 12 + (m - 1) + delta
  const ano = Math.floor(total / 12)
  const mes = (total % 12) + 1
  return `${ano}-${String(mes).padStart(2, '0')}`
}
