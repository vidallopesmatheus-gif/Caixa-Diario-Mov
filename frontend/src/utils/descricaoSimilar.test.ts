import { agruparPorDescricaoSimilar } from './descricaoSimilar'

interface Item {
  id: string
  descricao: string
  tipo: 'Entrada' | 'Saida'
  valor: number
}

function item(id: string, descricao: string, valor = -10): Item {
  return { id, descricao, tipo: 'Saida', valor }
}

test('não agrupa transações com prefixo comum longo mas destinatários diferentes (bug relatado)', () => {
  const itens = [
    item('1', 'Transferência enviada pelo Pix - OVOS UANDRA'),
    item('2', 'Transferência enviada pelo Pix - Rafael Abraao'),
    item('3', 'Transferência enviada pelo Pix - AUTOPASS S.A.'),
    item('4', 'Transferência enviada pelo Pix - Edson Gomes'),
    item('5', 'Transferência enviada pelo Pix - LUANA SILVA'),
    item('6', 'Transferência enviada pelo Pix - PRISCILA MACEDO'),
  ]

  const grupos = agruparPorDescricaoSimilar(itens)

  // Cada destinatário diferente deve virar seu próprio grupo — nenhum grupo com 2+ itens.
  expect(grupos).toHaveLength(6)
  expect(grupos.every(g => g.itens.length === 1)).toBe(true)
})

test('agrupa lançamentos realmente parecidos (mesmo favorecido, valores diferentes)', () => {
  const itens = [
    item('1', 'Transferência enviada pelo Pix - LUANA SILVA'),
    item('2', 'Transferência enviada pelo Pix - LUANA SILVA'),
    item('3', 'Transferência enviada pelo Pix - LUANA SILVA'),
  ]

  const grupos = agruparPorDescricaoSimilar(itens)

  expect(grupos).toHaveLength(1)
  expect(grupos[0].itens).toHaveLength(3)
})

test('usa CNPJ/CPF da descrição como identificador do favorecido quando presente', () => {
  const itens = [
    item('1', 'Pagamento Fornecedor 12.345.678/0001-90'),
    item('2', 'Pagamento Fornecedor 12.345.678/0001-90 ref 08/2026'),
    item('3', 'Pagamento Fornecedor 98.765.432/0001-10'),
  ]

  const grupos = agruparPorDescricaoSimilar(itens)

  const grupoDoc1 = grupos.find(g => g.itens.some(i => i.id === '1'))
  expect(grupoDoc1?.itens.map(i => i.id).sort()).toEqual(['1', '2'])
  const grupoDoc2 = grupos.find(g => g.itens.some(i => i.id === '3'))
  expect(grupoDoc2?.itens).toHaveLength(1)
})

test('não mistura entrada e saída no mesmo grupo mesmo com descrição idêntica', () => {
  const itens: Item[] = [
    { id: '1', descricao: 'Estorno', tipo: 'Entrada', valor: 50 },
    { id: '2', descricao: 'Estorno', tipo: 'Saida', valor: -50 },
  ]

  const grupos = agruparPorDescricaoSimilar(itens)

  expect(grupos).toHaveLength(2)
})

test('descrições curtas sem prefixo comum relevante não são agrupadas indevidamente', () => {
  const itens = [item('1', 'Tarifa'), item('2', 'Multa')]

  const grupos = agruparPorDescricaoSimilar(itens)

  expect(grupos).toHaveLength(2)
})
