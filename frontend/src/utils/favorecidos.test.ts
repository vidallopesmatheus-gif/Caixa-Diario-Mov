import { calcularRankingFavorecidos } from './favorecidos'

test('agrupa por favorecido usando o prefixo removido como rótulo legível', () => {
  const itens = [
    { data: '2026-08-05', descricao: 'Compra no débito - COVABRA SUPERMERCADOS', valor: 100 },
    { data: '2026-08-12', descricao: 'Compra no débito - COVABRA SUPERMERCADOS', valor: 50 },
    { data: '2026-08-20', descricao: 'Compra no débito - POSTO SHELL', valor: 200 },
  ]

  const ranking = calcularRankingFavorecidos(itens, 'Saida')

  expect(ranking).toHaveLength(2)
  const covabra = ranking.find(r => r.rotulo === 'COVABRA SUPERMERCADOS')
  expect(covabra).toBeDefined()
  expect(covabra!.total).toBe(150)
  expect(covabra!.ocorrencias).toBe(2)
})

test('usa CNPJ como chave de agrupamento e ainda produz um rótulo legível (não "DOC:...")', () => {
  const itens = [
    { data: '2026-08-01', descricao: 'Transferência enviada pelo Pix - AUTOPASS S.A. - 07.140.538/0001-40', valor: 30 },
    { data: '2026-08-15', descricao: 'Transferência enviada pelo Pix - AUTOPASS S.A. - 07.140.538/0001-40', valor: 30 },
    { data: '2026-08-02', descricao: 'Transferência enviada pelo Pix - OUTRO FAVORECIDO - 11.222.333/0001-44', valor: 10 },
  ]

  const ranking = calcularRankingFavorecidos(itens, 'Saida')

  const autopass = ranking.find(r => r.total === 60)
  expect(autopass).toBeDefined()
  expect(autopass!.ocorrencias).toBe(2)
  expect(autopass!.rotulo).not.toContain('DOC:')
})

test('ordena por total decrescente e respeita o limite', () => {
  const itens = [
    { data: '2026-08-01', descricao: 'Compra no débito - MERCADO A', valor: 10 },
    { data: '2026-08-01', descricao: 'Compra no débito - MERCADO B', valor: 999 },
    { data: '2026-08-01', descricao: 'Compra no débito - MERCADO C', valor: 50 },
  ]

  const ranking = calcularRankingFavorecidos(itens, 'Saida', 2)

  expect(ranking).toHaveLength(2)
  expect(ranking[0].total).toBe(999)
  expect(ranking[1].total).toBe(50)
})

test('lista vazia retorna ranking vazio', () => {
  expect(calcularRankingFavorecidos([], 'Entrada')).toEqual([])
})
