import { ehOperacional } from './lancamentos'

test('exclui Transferencia, Rendimento, Investimento, Financiamento e PagamentoFatura', () => {
  expect(ehOperacional({ tipoCusto: 'Transferencia' })).toBe(false)
  expect(ehOperacional({ tipoCusto: 'Rendimento' })).toBe(false)
  expect(ehOperacional({ tipoCusto: 'Investimento' })).toBe(false)
  expect(ehOperacional({ tipoCusto: 'Financiamento' })).toBe(false)
  expect(ehOperacional({ tipoCusto: 'PagamentoFatura' })).toBe(false)
})

test('considera operacional Receita, CustoFixo, CustoVariavel e sem tipoCusto', () => {
  expect(ehOperacional({ tipoCusto: 'Receita' })).toBe(true)
  expect(ehOperacional({ tipoCusto: 'CustoFixo' })).toBe(true)
  expect(ehOperacional({ tipoCusto: 'CustoVariavel' })).toBe(true)
  expect(ehOperacional({})).toBe(true)
})
