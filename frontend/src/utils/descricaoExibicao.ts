// Limpa a descrição bruta do extrato bancário (sempre cheia de ruído: CPF mascarado, código do
// banco, agência/conta) pra uma versão curta e legível — a descrição original nunca é perdida,
// só deixa de ser a exibida de cara (ver tooltip/painel de edição, que sempre mostram a bruta).

export interface DescricaoExibicao {
  titulo: string
  subtitulo?: string
}

/** "BANCO INTER" -> "Banco Inter"; "Juliana Lima Moraes" (já mista) fica como está. */
function titleCaseSeGritando(s: string): string {
  const texto = s.trim()
  if (!texto) return texto
  const temMinuscula = /[a-zà-ú]/.test(texto)
  if (temMinuscula) return texto // já não está tudo em caixa alta, não arrisca mexer
  return texto.toLowerCase().replace(/\b\p{L}/gu, c => c.toUpperCase())
}

function extrairBanco(resto: string): string {
  const semAgencia = resto.split(/Agência:/i)[0].trim()
  const semCodigo = semAgencia.replace(/\(\d+\)\s*$/, '').trim()
  return titleCaseSeGritando(semCodigo)
}

const PIX_ENVIADO = /^Transferência enviada pelo Pix - (.+?) - [•*\d.-]+ - (.+)$/i
const PIX_RECEBIDO = /^Transferência recebida pelo Pix - (.+?) - [•*\d.-]+ - (.+)$/i
const DEBITO_VIA = /^Compra no débito via \w+ - (.+)$/i
const DEBITO_PLANO = /^Compra no débito - (.+)$/i
const APLICACAO = /^Aplicaç(?:ão|ões)\s+(.+)$/i
const RESGATE = /^Resgate\s+(.+)$/i

/** Deriva um título curto + subtítulo opcional pra exibição; nunca altera a descrição salva. */
export function limparDescricao(descricaoBruta: string): DescricaoExibicao {
  const bruta = descricaoBruta ?? ''

  let m = bruta.match(PIX_ENVIADO)
  if (m) return { titulo: `Pix enviado · ${titleCaseSeGritando(m[1])}`, subtitulo: extrairBanco(m[2]) }

  m = bruta.match(PIX_RECEBIDO)
  if (m) return { titulo: `Pix recebido · ${titleCaseSeGritando(m[1])}`, subtitulo: extrairBanco(m[2]) }

  m = bruta.match(DEBITO_VIA)
  if (m) return { titulo: `Débito · ${titleCaseSeGritando(m[1].trim())}` }

  m = bruta.match(DEBITO_PLANO)
  if (m) return { titulo: `Débito · ${titleCaseSeGritando(m[1].trim())}` }

  m = bruta.match(APLICACAO)
  if (m) return { titulo: `Guardado no ${m[1].trim()}` }

  m = bruta.match(RESGATE)
  if (m) return { titulo: `Resgatado do ${m[1].trim()}` }

  return { titulo: bruta }
}
