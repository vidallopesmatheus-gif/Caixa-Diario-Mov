import { agruparPorDescricaoSimilar, mascararDocumentos, criterioRegra } from './descricaoSimilar'

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

describe('mascararDocumentos', () => {
  test('mascara CPF formatado, mantendo os dois blocos do meio', () => {
    const resultado = mascararDocumentos('Pix recebido - NOME COMPLETO - 111.222.333-44')
    expect(resultado).toBe('Pix recebido - NOME COMPLETO - •••.222.333-••')
    expect(resultado).not.toContain('111')
    expect(resultado).not.toContain('44')
  })

  test('mascara CPF sem formatação (só dígitos)', () => {
    expect(mascararDocumentos('Doc 11122233344')).toBe('Doc •••.222.333-••')
  })

  test('mascara CNPJ formatado, mantendo os dois blocos do meio', () => {
    const resultado = mascararDocumentos('Pagamento Fornecedor 12.345.678/0001-90')
    expect(resultado).toBe('Pagamento Fornecedor ••.345.678/0001-••')
    expect(resultado).not.toContain('12.')
    expect(resultado).not.toContain('-90')
  })

  test('mascara CNPJ sem formatação (só dígitos)', () => {
    expect(mascararDocumentos('Doc 12345678000190')).toBe('Doc ••.345.678/0001-••')
  })

  test('não altera texto sem CPF/CNPJ', () => {
    expect(mascararDocumentos('Compra no débito - POSTO SHELL')).toBe('Compra no débito - POSTO SHELL')
  })

  test('não quebra um CPF já mascarado pelo próprio banco (sem dígitos suficientes pra casar)', () => {
    const texto = 'Transferência recebida pelo Pix - NOME COMPLETO - •••.123.456-•• - BCO C6 S.A.'
    expect(mascararDocumentos(texto)).toBe(texto)
  })

  test('mascara CNPJ e CPF presentes na mesma string, sem interferência entre os dois', () => {
    const resultado = mascararDocumentos('De 12.345.678/0001-90 para 111.222.333-44')
    expect(resultado).toBe('De ••.345.678/0001-•• para •••.222.333-••')
  })
})

describe('criterioRegra', () => {
  test('usa o documento como chave quando a descrição tem CNPJ/CPF', () => {
    const chave = criterioRegra('Saida', 'Pagamento Fornecedor 12.345.678/0001-90 ref 08/2026')
    expect(chave).toBe('Saida::DOC:12345678000190')
  })

  test('duas descrições com o mesmo documento mas texto diferente geram a mesma chave', () => {
    const chave1 = criterioRegra('Saida', 'Pagamento Fornecedor 12.345.678/0001-90 ref 08/2026')
    const chave2 = criterioRegra('Saida', 'Pagamento Fornecedor 12.345.678/0001-90 ref 09/2026')
    expect(chave1).toBe(chave2)
  })

  test('usa a descrição normalizada (maiúscula, sem espaço nas pontas) quando não há documento', () => {
    expect(criterioRegra('Entrada', '  aplicação rdb  ')).toBe('Entrada::APLICAÇÃO RDB')
  })

  test('mesmo critério com tipos diferentes (Entrada vs Saida) gera chaves diferentes', () => {
    const chaveEntrada = criterioRegra('Entrada', 'Tarifa')
    const chaveSaida = criterioRegra('Saida', 'Tarifa')
    expect(chaveEntrada).not.toBe(chaveSaida)
  })
})
