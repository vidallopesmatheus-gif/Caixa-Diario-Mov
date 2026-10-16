import { fmtBRL, fmtPct, fmtDate, todayISO, addDays, monthLabel, fmtBRLCompacto } from './format'

test('fmtBRL formata valor em reais', () => {
  expect(fmtBRL(1234.5)).toBe('R$ 1.234,50')
})

// toLocaleString pt-BR usa espaço NÃO separável (U+00A0) entre "R$" e o número neste ambiente de
// teste — normaliza pra espaço comum antes de comparar (mesmo texto visualmente, bytes diferentes).
const semNbsp = (s: string) => s.replace(/\u00A0/g, ' ')

// Redesign visual (Money Vision): valor abreviado pra eixos/rótulos de gráfico.
test('fmtBRLCompacto abrevia mil e milhão, mantém valor cheio abaixo de mil', () => {
  expect(semNbsp(fmtBRLCompacto(500))).toBe('R$ 500,00')
  expect(semNbsp(fmtBRLCompacto(12400))).toBe('R$ 12,4 mil')
  expect(semNbsp(fmtBRLCompacto(-12400))).toBe('-R$ 12,4 mil')
  expect(semNbsp(fmtBRLCompacto(3200000))).toBe('R$ 3,2 mi')
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

test('fmtBRL arredonda dízimas de ponto flutuante', () => {
  expect(fmtBRL(5560.079999999999)).toContain('5.560,08')
})

test('fmtPct formata com vírgula decimal, não ponto', () => {
  expect(fmtPct(12.3)).toBe('12,3%')
})

test('fmtPct aceita número de casas decimais customizado', () => {
  expect(fmtPct(12.345, 2)).toBe('12,35%')
})
