import { calcularDiasAtraso, deslocarMes, estaNoMes, labelMes } from './contasPagarReceber'

test('deslocarMes avança e recua, inclusive virando o ano', () => {
  expect(deslocarMes('2026-10', 1)).toBe('2026-11')
  expect(deslocarMes('2026-12', 1)).toBe('2027-01')
  expect(deslocarMes('2026-01', -1)).toBe('2025-12')
})

test('labelMes formata "mês/ano" abreviado', () => {
  expect(labelMes('2026-10')).toBe('Out/2026')
})

test('estaNoMes compara só ano-mês', () => {
  expect(estaNoMes('2026-10-15', '2026-10')).toBe(true)
  expect(estaNoMes('2026-11-01', '2026-10')).toBe(false)
})

test('calcularDiasAtraso: zero quando pago, sem vencimento ou vencimento futuro/hoje', () => {
  expect(calcularDiasAtraso('2026-10-01', true, '2026-10-10')).toBe(0)
  expect(calcularDiasAtraso(null, false, '2026-10-10')).toBe(0)
  expect(calcularDiasAtraso('2026-10-10', false, '2026-10-10')).toBe(0)
  expect(calcularDiasAtraso('2026-10-15', false, '2026-10-10')).toBe(0)
})

test('calcularDiasAtraso: dias corridos entre o vencimento e hoje quando vencida', () => {
  expect(calcularDiasAtraso('2026-10-05', false, '2026-10-10')).toBe(5)
})
