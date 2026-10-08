import { beforeEach, afterEach, vi } from 'vitest'
import { calcularJanelaPeriodo } from './periodo'

beforeEach(() => {
  vi.useFakeTimers()
})

afterEach(() => {
  vi.useRealTimers()
})

function fixarHoje(iso: string) {
  // meio-dia UTC evita cair no dia anterior/seguinte por fuso horário
  vi.setSystemTime(new Date(`${iso}T12:00:00Z`))
}

test('mes: com o mes em andamento, compara com os mesmos N dias do mes anterior', () => {
  fixarHoje('2026-10-08') // dia 8 de outubro
  const janela = calcularJanelaPeriodo('mes')

  expect(janela.de).toBe('2026-10-01')
  expect(janela.ate).toBe('2026-10-31')
  expect(janela.deAnterior).toBe('2026-09-01')
  // mesmos 8 dias de setembro, nao o mes de setembro inteiro (que tem 30 dias)
  expect(janela.ateAnterior).toBe('2026-09-08')
})

test('mes: mes anterior mais curto que o dia atual trunca no ultimo dia dele', () => {
  fixarHoje('2026-03-31') // marco tem 31 dias, fevereiro so tem 28 (2026 nao e bissexto)
  const janela = calcularJanelaPeriodo('mes')

  expect(janela.deAnterior).toBe('2026-02-01')
  expect(janela.ateAnterior).toBe('2026-02-28')
})

test('mes: ultimo dia do mes, ambos os meses com 31 dias, compara mes cheio com mes cheio', () => {
  fixarHoje('2026-08-31') // agosto e julho tem 31 dias cada
  const janela = calcularJanelaPeriodo('mes')

  expect(janela.deAnterior).toBe('2026-07-01')
  expect(janela.ateAnterior).toBe('2026-07-31')
})

test('personalizado: compara com a mesma quantidade de dias imediatamente anterior', () => {
  fixarHoje('2026-06-15')
  const janela = calcularJanelaPeriodo('personalizado', { de: '2026-06-01', ate: '2026-06-10' })

  expect(janela.deAnterior).toBe('2026-05-22')
  expect(janela.ateAnterior).toBe('2026-05-31')
})
