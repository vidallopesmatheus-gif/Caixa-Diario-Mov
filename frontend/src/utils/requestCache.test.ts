import { beforeEach, afterEach, vi, test, expect } from 'vitest'
import { cachedFetch, invalidarCache } from './requestCache'

beforeEach(() => {
  vi.useFakeTimers()
  invalidarCache()
})

afterEach(() => {
  vi.useRealTimers()
})

test('chamadas concorrentes com a mesma chave compartilham uma promise só', async () => {
  let chamadas = 0
  const buscar = () => { chamadas++; return Promise.resolve(42) }

  const [a, b] = await Promise.all([cachedFetch('x', buscar), cachedFetch('x', buscar)])

  expect(a).toBe(42)
  expect(b).toBe(42)
  expect(chamadas).toBe(1)
})

test('chamada seguinte dentro do TTL reaproveita o resultado em cache', async () => {
  let chamadas = 0
  const buscar = () => { chamadas++; return Promise.resolve(chamadas) }

  const primeira = await cachedFetch('y', buscar, 10_000)
  const segunda = await cachedFetch('y', buscar, 10_000)

  expect(primeira).toBe(1)
  expect(segunda).toBe(1)
  expect(chamadas).toBe(1)
})

test('chamada depois do TTL expirar busca de novo', async () => {
  let chamadas = 0
  const buscar = () => { chamadas++; return Promise.resolve(chamadas) }

  await cachedFetch('z', buscar, 1_000)
  vi.advanceTimersByTime(1_001)
  const depois = await cachedFetch('z', buscar, 1_000)

  expect(depois).toBe(2)
  expect(chamadas).toBe(2)
})

test('chaves diferentes nunca compartilham cache', async () => {
  const resultado1 = await cachedFetch('a', () => Promise.resolve('um'))
  const resultado2 = await cachedFetch('b', () => Promise.resolve('dois'))

  expect(resultado1).toBe('um')
  expect(resultado2).toBe('dois')
})

test('invalidarCache sem prefixo limpa tudo', async () => {
  let chamadas = 0
  const buscar = () => { chamadas++; return Promise.resolve(chamadas) }

  await cachedFetch('k', buscar, 60_000)
  invalidarCache()
  const depois = await cachedFetch('k', buscar, 60_000)

  expect(depois).toBe(2)
})

test('invalidarCache com prefixo só limpa as chaves que começam com ele', async () => {
  await cachedFetch('dre:cliente1:2026-10', () => Promise.resolve('a'), 60_000)
  await cachedFetch('evolucao:cliente1', () => Promise.resolve('b'), 60_000)

  invalidarCache('dre:')

  let chamadasDre = 0
  let chamadasEvolucao = 0
  await cachedFetch('dre:cliente1:2026-10', () => { chamadasDre++; return Promise.resolve('a2') }, 60_000)
  await cachedFetch('evolucao:cliente1', () => { chamadasEvolucao++; return Promise.resolve('b2') }, 60_000)

  expect(chamadasDre).toBe(1) // foi invalidada, buscou de novo
  expect(chamadasEvolucao).toBe(0) // ainda em cache, não buscou de novo
})

test('erro na busca não fica em cache — próxima chamada tenta de novo', async () => {
  let chamadas = 0
  const buscar = () => {
    chamadas++
    return chamadas === 1 ? Promise.reject(new Error('falhou')) : Promise.resolve('ok')
  }

  await expect(cachedFetch('e', buscar)).rejects.toThrow('falhou')
  const resultado = await cachedFetch('e', buscar)

  expect(resultado).toBe('ok')
  expect(chamadas).toBe(2)
})
