import { calcularRitmoMeta } from './metaRitmo'
import type { MetaAnual } from '../types'

function baseMeta(overrides: Partial<MetaAnual> = {}): MetaAnual {
  return {
    id: '1', clienteId: 'c1', ano: 2026,
    metaReceita: 0, metaLucro: 0, mesInicio: 1, periodoMeses: 12,
    salvoEm: '2026-01-01T00:00:00.000Z',
    modoMeta: 'metodo',
    valorSonho: 10000, prazoAnos: 0, taxaRetorno: 12, totalInvestido: 0,
    ...overrides,
  }
}

test('sem valorSonho, retorna sem-dados', () => {
  const ritmo = calcularRitmoMeta(baseMeta({ valorSonho: 0 }))
  expect(ritmo.status).toBe('sem-dados')
})

test('investido >= valorSonho, retorna atingida', () => {
  const ritmo = calcularRitmoMeta(baseMeta({ valorSonho: 5000, totalInvestido: 6000 }))
  expect(ritmo.status).toBe('atingida')
})

test('usa dataAlvo explícita como fonte de verdade quando presente', () => {
  const ritmo = calcularRitmoMeta(baseMeta({
    salvoEm: '2026-01-01T00:00:00.000Z',
    dataAlvo: '2027-01-01',
    valorSonho: 10000, totalInvestido: 0, taxaRetorno: 12,
  }))
  expect(ritmo.dataAlvo?.getFullYear()).toBe(2027)
  expect(ritmo.dataAlvo?.getMonth()).toBe(0)
})

test('sem dataAlvo, cai no prazoAnos relativo como fallback (metas antigas)', () => {
  const ritmo = calcularRitmoMeta(baseMeta({
    salvoEm: '2026-06-15T12:00:00.000Z',
    dataAlvo: undefined,
    prazoAnos: 2,
    valorSonho: 10000, totalInvestido: 0, taxaRetorno: 12,
  }))
  expect(ritmo.dataAlvo?.getFullYear()).toBe(2028)
})

test('sem dataAlvo nem prazoAnos, dataAlvo fica nula e status sem-dados', () => {
  const ritmo = calcularRitmoMeta(baseMeta({
    dataAlvo: undefined,
    prazoAnos: 0,
    valorSonho: 10000, totalInvestido: 0, taxaRetorno: 12,
  }))
  expect(ritmo.dataAlvo).toBeNull()
  expect(ritmo.status).toBe('sem-dados')
})

test('objetivo de curto prazo (poucos meses) calcula ritmo corretamente com dataAlvo precisa', () => {
  // Regressão do bug estrutural: prazoAnos (inteiro, em anos) não conseguia representar um
  // objetivo de poucos meses — dataAlvo em dias/meses exatos resolve isso.
  const ritmo = calcularRitmoMeta(baseMeta({
    salvoEm: '2026-01-01T00:00:00.000Z',
    dataAlvo: '2026-05-01',
    valorSonho: 5000, totalInvestido: 1000, taxaRetorno: 10,
  }))
  expect(ritmo.dataAlvo?.getMonth()).toBe(4)
  expect(ritmo.status).not.toBe('sem-dados')
})
