import { calcularConcentracaoPorDia } from './concentracao'

function reg(data: string, entradas: number[] = [], saidas: number[] = [], tipoCusto?: string) {
  return {
    data,
    entradas: entradas.map(valor => ({ valor, tipoCusto })),
    saidas: saidas.map(valor => ({ valor, tipoCusto })),
  }
}

test('granularidade "dia": encontra o dia da semana e o dia do mês com maior volume', () => {
  // 2026-08-01 é sábado; 2026-08-03 é segunda.
  const registros = [
    reg('2026-08-01', [100]),
    reg('2026-08-08', [50]), // outro sábado, mesmo dia da semana
    reg('2026-08-03', [10]),
  ]

  const resultado = calcularConcentracaoPorDia(registros, 'dia')

  expect(resultado.granularidade).toBe('dia')
  expect(resultado.entradaDiaSemana?.rotulo).toBe('Sábado')
  expect(resultado.entradaDiaSemana?.total).toBe(150)
  expect(resultado.entradaMes).toBeNull()
})

test('granularidade "mes": encontra o mês com maior entrada e maior saída', () => {
  const registros = [
    reg('2026-06-01', [100], []),
    reg('2026-07-01', [300], []),
    reg('2026-08-01', [], [500]),
  ]

  const resultado = calcularConcentracaoPorDia(registros, 'mes')

  expect(resultado.granularidade).toBe('mes')
  expect(resultado.entradaMes?.rotulo).toBe('Jul/2026')
  expect(resultado.saidaMes?.rotulo).toBe('Ago/2026')
  expect(resultado.entradaDiaSemana).toBeNull()
})

test('exclui transferências e rendimento do cálculo, mesma regra do DRE', () => {
  const registros = [
    reg('2026-08-01', [1000], [], 'Transferencia'),
    reg('2026-08-02', [50], [], 'Receita'),
  ]

  const resultado = calcularConcentracaoPorDia(registros, 'dia')

  expect(resultado.entradaDiaMes?.total).toBe(50)
})

test('sem lançamentos operacionais, retorna null pros pontos', () => {
  const resultado = calcularConcentracaoPorDia([], 'dia')

  expect(resultado.entradaDiaSemana).toBeNull()
  expect(resultado.saidaDiaSemana).toBeNull()
})
