import { limparDescricao } from './descricaoExibicao'

test('Pix enviado extrai nome do destinatário e banco como subtítulo', () => {
  const r = limparDescricao(
    'Transferência enviada pelo Pix - Rafael Abraao Debrieri - •••.605.478-•• - BANCO INTER (0077) Agência: 1 Conta: 14849694-6',
  )
  expect(r.titulo).toBe('Pix enviado · Rafael Abraao Debrieri')
  expect(r.subtitulo).toBe('Banco Inter')
})

test('Pix recebido extrai nome do remetente', () => {
  const r = limparDescricao(
    'Transferência recebida pelo Pix - ALZIRA MOTTA - •••.158.369-•• - COOP SICREDI ALT SERRA RS/SC Agência: 268 Conta: 11379-1',
  )
  expect(r.titulo).toBe('Pix recebido · Alzira Motta')
  expect(r.subtitulo).toBe('Coop Sicredi Alt Serra Rs/Sc')
})

test('Compra no débito via app (NuPay) extrai só o estabelecimento', () => {
  const r = limparDescricao('Compra no débito via NuPay - Uber')
  expect(r.titulo).toBe('Débito · Uber')
  expect(r.subtitulo).toBeUndefined()
})

test('Compra no débito simples capitaliza o nome em caixa alta', () => {
  const r = limparDescricao('Compra no débito - PADARIA CASA MOSTRA')
  expect(r.titulo).toBe('Débito · Padaria Casa Mostra')
})

test('Compra no débito não remaiusculiza nome já em caixa mista', () => {
  const r = limparDescricao('Compra no débito - Mercado Bom Preço')
  expect(r.titulo).toBe('Débito · Mercado Bom Preço')
})

test('Aplicação em RDB vira "Guardado no RDB"', () => {
  expect(limparDescricao('Aplicação RDB').titulo).toBe('Guardado no RDB')
})

test('Resgate de RDB vira "Resgatado do RDB"', () => {
  expect(limparDescricao('Resgate RDB').titulo).toBe('Resgatado do RDB')
})

test('Descrição sem padrão reconhecido mantém o texto original', () => {
  const bruta = 'Pagamento de boleto - Companhia de Água'
  expect(limparDescricao(bruta).titulo).toBe(bruta)
  expect(limparDescricao(bruta).subtitulo).toBeUndefined()
})
