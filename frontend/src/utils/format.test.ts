import { fmtBRL, fmtDate, todayISO, addDays, monthLabel } from './format'

test('fmtBRL formata valor em reais', () => {
  expect(fmtBRL(1234.5)).toBe('R$ 1.234,50')
})

test('fmtDate converte ISO para dd/mm/yyyy', () => {
  expect(fmtDate('2026-05-15')).toBe('15/05/2026')
})

test('addDays avança dias corretamente', () => {
  expect(addDays('2026-05-15', 1)).toBe('2026-05-16')
  expect(addDays('2026-05-15', -1)).toBe('2026-05-14')
})

test('addDays atravessa virada de mês', () => {
  expect(addDays('2026-01-31', 1)).toBe('2026-02-01')
})

test('monthLabel retorna mês abreviado', () => {
  expect(monthLabel('2026-05-15')).toBe('Mai/2026')
})

test('todayISO retorna formato YYYY-MM-DD', () => {
  expect(todayISO()).toMatch(/^\d{4}-\d{2}-\d{2}$/)
})
